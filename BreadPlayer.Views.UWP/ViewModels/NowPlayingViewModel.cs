﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using Windows.UI.Core;
using BreadPlayer.Core;
using BreadPlayer.Core.Common;
using BreadPlayer.Core.Events;
using BreadPlayer.Database;
using BreadPlayer.Extensions;
using BreadPlayer.Web.Lastfm;
using IF.Lastfm.Core.Api;
using IF.Lastfm.Core.Objects;
using BreadPlayer.Dispatcher;

namespace BreadPlayer.ViewModels
{
    public class NowPlayingViewModel : ViewModelBase
    {
        #region Loading Properties

        private bool _artistInfoLoading;
        public bool ArtistInfoLoading { get => _artistInfoLoading; set => Set(ref _artistInfoLoading, value); }
        private bool _albumInfoLoading;
        public bool AlbumInfoLoading { get => _albumInfoLoading; set => Set(ref _albumInfoLoading, value); }
        private bool _artistFetchFailed;
        public bool ArtistFetchFailed { get => _artistFetchFailed; set => Set(ref _artistFetchFailed, value); }
        private bool _albumFetchFailed;
        public bool AlbumFetchFailed { get => _albumFetchFailed; set => Set(ref _albumFetchFailed, value); }
        #endregion

        private LibraryService _service = new LibraryService(new DocumentStoreDatabaseService(SharedLogic.DatabasePath, "Tracks"));
        public string CorrectArtist { get; set; }
        public string CorrectAlbum { get; set; }
        private string _artistBio;
        public string ArtistBio
        {
            get => _artistBio;
            set => Set(ref _artistBio, value);
        }

        private ThreadSafeObservableCollection<LastTrack> _albumTracks;
        public ThreadSafeObservableCollection<LastTrack> AlbumTracks
        {
            get => _albumTracks;
            set => Set(ref _albumTracks, value);
        }

        private ThreadSafeObservableCollection<LastArtist> _similarArtists;
        public ThreadSafeObservableCollection<LastArtist> SimilarArtists
        {
            get => _similarArtists;
            set => Set(ref _similarArtists, value);
        }
        public ICommand RetryCommand { get; set; }
        private LastfmClient LastfmClient => new Lastfm().LastfmClient;

        public NowPlayingViewModel()
        {
            RetryCommand = new RelayCommand(Retry);

            //the work around to knowing when the new song has started.
            //the event is needed to update the bio etc.
            SharedLogic.Player.MediaChanging += (sender, e) =>
            {
                SharedLogic.Player.MediaStateChanged += Player_MediaStateChanged;
            };
        }
        private async void Retry(object para)
        {
            if (string.IsNullOrEmpty(CorrectArtist))
            {
                return;
            }

            if (para.ToString() == "Artist")
            {
                await GetArtistInfo(CorrectArtist);
                SharedLogic.Player.CurrentlyPlayingFile.LeadArtist = CorrectArtist;
            }
            else if (para.ToString() == "Album")
            {
                if (string.IsNullOrEmpty(CorrectAlbum))
                {
                    return;
                }

                await GetAlbumInfo(CorrectArtist, CorrectAlbum);
                SharedLogic.Player.CurrentlyPlayingFile.LeadArtist = CorrectArtist;
                SharedLogic.Player.CurrentlyPlayingFile.Album = CorrectAlbum;
            }
            await _service.UpdateMediafile(SharedLogic.Player.CurrentlyPlayingFile);
        }
        private async void Player_MediaStateChanged(object sender, MediaStateChangedEventArgs e)
        {
            if (e.NewState == PlayerState.Playing)
            {
                SharedLogic.Player.MediaStateChanged -= Player_MediaStateChanged;
                await GetInfo(SharedLogic.Player.CurrentlyPlayingFile?.LeadArtist, SharedLogic.Player.CurrentlyPlayingFile?.Album);
            }
        }

        private int _retries;
        private async Task GetInfo(string artistName, string albumName)
        {
            try
            {
                //start the tasks on another thread so that the UI doesn't hang.

                ConnectionProfile internetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();
                
                if (internetConnectionProfile != null)
                {
                    //cancel any previous requests
                    LastfmClient.HttpClient.CancelPendingRequests();
                    //start both tasks
                    await GetArtistInfo(artistName.ScrubGarbage().GetTag()).ConfigureAwait(false);
                    await GetAlbumInfo(artistName.ScrubGarbage().GetTag(), albumName.ScrubGarbage().GetTag()).ConfigureAwait(false);
                }
                else
                {
                    AlbumFetchFailed = true;
                    ArtistFetchFailed = true;
                }
            }
            catch (Exception)
            {
                //we use this simple logic to avoid too many retries.
                //MAX_RETRIES = 10;
                if (_retries == 10)
                {
                    //increase retry count
                    _retries++;

                    //retry
                    await GetInfo(artistName, albumName);
                }
            }
        }
        private async Task GetArtistInfo(string artistName)
        {
            await BreadDispatcher.InvokeAsync(async () =>
            {
                LastfmClient.Artist.HttpClient.CancelPendingRequests();
                //CheckAndCancelOperation(ArtistInfoOperation, token);
                ArtistInfoLoading = true;
                var artistInfoResponse = await LastfmClient.Artist.GetInfoAsync(artistName, "en", true).ConfigureAwait(false);
                ArtistBio = "";
                ArtistFetchFailed = false;
                if (artistInfoResponse.Success)
                {
                    LastArtist artist = artistInfoResponse.Content;
                    ArtistBio = artist.Bio.Content.ScrubHtml();
                    SimilarArtists = new ThreadSafeObservableCollection<LastArtist>(artist.Similar);
                }
                else
                {
                    ArtistFetchFailed = true;
                    ArtistInfoLoading = false;
                }
                //if it is empty or it starts with [unknown],
                //which is the identifier for unknown artists;
                //just fail.
                if (string.IsNullOrEmpty(ArtistBio) || ArtistBio.StartsWith("[unknown]") || ArtistBio.StartsWith("This is not an artist"))
                {
                    ArtistFetchFailed = true;
                }

                ArtistInfoLoading = false;
            });
        }
        private async Task GetAlbumInfo(string artistName, string albumName)
        {
            await BreadDispatcher.InvokeAsync(async () =>
            {
                LastfmClient.Album.HttpClient.CancelPendingRequests();
                //CheckAndCancelOperation(AlbumInfoOperation, token);
                AlbumInfoLoading = true;
                AlbumTracks?.Clear();
                AlbumFetchFailed = false;
                var albumInfoResponse = await LastfmClient.Album.GetInfoAsync(artistName, albumName, true).ConfigureAwait(false);
                if (albumInfoResponse.Success)
                {
                    LastAlbum album = albumInfoResponse.Content;
                    AlbumTracks = new ThreadSafeObservableCollection<LastTrack>(album.Tracks);
                }
                else
                {
                    AlbumFetchFailed = true;
                    AlbumInfoLoading = false;
                }
                if (AlbumTracks?.Any() == false)
                {
                    AlbumFetchFailed = true;
                }

                AlbumInfoLoading = false;
            });
        }     
    }
}
