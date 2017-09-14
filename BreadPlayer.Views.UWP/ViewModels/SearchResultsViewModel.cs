﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BreadPlayer.Core;
using BreadPlayer.Core.Models;
using BreadPlayer.Database;

namespace BreadPlayer.ViewModels
{
    public class SearchResultsViewModel : ViewModelBase
    {
        private bool _albumsVisible;
        public bool AlbumsVisible
        {
            get => _albumsVisible;
            set => Set(ref _albumsVisible, value);
        }
        private bool _artistsVisible;
        public bool ArtistsVisible
        {
            get => _artistsVisible;
            set => Set(ref _artistsVisible, value);
        }
        private bool _toastsVisible;
        public bool ToastsVisible
        {
            get => _toastsVisible;
            set => Set(ref _toastsVisible, value);
        }
        private ThreadSafeObservableCollection<Mediafile> _querySongs;
        public ThreadSafeObservableCollection<Mediafile> QuerySongs
        {
            get => _querySongs;
            set => Set(ref _querySongs, value);
        }

        private ThreadSafeObservableCollection<Album> _queryAlbums;
        public ThreadSafeObservableCollection<Album> QueryAlbums
        {
            get => _queryAlbums;
            set => Set(ref _queryAlbums, value);
        }

        private ThreadSafeObservableCollection<Artist> _queryArtists;
        public ThreadSafeObservableCollection<Artist> QueryArtists
        {
            get => _queryArtists;
            set => Set(ref _queryArtists, value);
        }
        public async Task<(IEnumerable<Mediafile> Songs, IEnumerable<Album> Albums, IEnumerable<Artist> Artists)> StartSearch(string query)
        {
            var documentStore = new DocumentStoreDatabaseService(SharedLogic.DatabasePath, "Tracks");
            LibraryService service = new LibraryService(documentStore);
            AlbumArtistService albumArtistService = new AlbumArtistService(documentStore);
            return (await service.Query(query, 5),
                    await albumArtistService.QueryAlbumsAsync(query, 10),
                    await albumArtistService.QueryArtistsAsync(query, 10));
        }
        public async Task GetAlbumsAndTracks(string query)
        {
            if (!string.IsNullOrEmpty(query))
            {
                QueryAlbums = new ThreadSafeObservableCollection<Album>();
                QuerySongs = new ThreadSafeObservableCollection<Mediafile>();
                QueryArtists = new ThreadSafeObservableCollection<Artist>();

                var queryresults = (await StartSearch(query.ToLower()));
                if (queryresults.Songs != null)
                {
                    ToastsVisible = true;
                    QuerySongs.AddRange(queryresults.Songs);
                }
                if (queryresults.Albums != null)
                {
                    AlbumsVisible = true;
                    QueryAlbums.AddRange(queryresults.Albums);
                }
                if (queryresults.Artists != null)
                {
                    ArtistsVisible = true;
                    QueryArtists.AddRange(queryresults.Artists);
                }
            }
        }
    }
}
