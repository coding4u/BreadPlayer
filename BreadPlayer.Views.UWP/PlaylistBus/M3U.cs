﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using BreadPlayer.Core;
using BreadPlayer.Core.Models;
using BreadPlayer.Database;
using BreadPlayer.ViewModels;
using BreadPlayer.Messengers;
using BreadPlayer.Helpers;

namespace BreadPlayer.PlaylistBus
{
    public class M3U : IPlaylist
    {
        public async Task<IEnumerable<Mediafile>> LoadPlaylist(StorageFile file)
        {
            using (var streamReader = new StreamReader(await file.OpenStreamForReadAsync()))
            {
                List<Mediafile> playlistSongs = new List<Mediafile>();
                string line;
                int index = 0;
                int failedFiles = 0;
                bool ext = false;
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (line.ToLower() == "#extm3u") //m3u header
                    {
                        ext = true;
                    }
                    else if (ext && line.ToLower().StartsWith("#extinf:")) //extinfo of each song
                    {
                        continue;
                    }
                    else if (line.StartsWith("#") || line == "") //pass blank lines
                    {
                        continue;
                    }
                    else
                    {
                        await Task.Run(async () =>
                        {
                            try
                            {
                                index++;
                                FileInfo info = new FileInfo(file.Path);//get playlist file info to get directory path
                                string path = line;
                                if (!File.Exists(line) && line[1] != ':') // if file doesn't exist then perhaps the path is relative
                                {
                                    path = info.DirectoryName + line; //add directory path to song path.
                                }

                                var accessFile = await StorageFile.GetFileFromPathAsync(path);
                                var token = StorageApplicationPermissions.FutureAccessList.Add(accessFile);

                                Mediafile mp3File = await TagReaderHelper.CreateMediafile(accessFile); //prepare Mediafile

                                await SettingsViewModel.SaveSingleFileAlbumArtAsync(mp3File,accessFile);
                                playlistSongs.Add(mp3File);
                                StorageApplicationPermissions.FutureAccessList.Remove(token);
                            }
                            catch
                            {
                                failedFiles++;
                            }
                        });
                    }                    
                }
                return playlistSongs;
            }
        }

        public async Task<bool> SavePlaylist(IEnumerable<Mediafile> songs)
        {
            FileSavePicker picker = new FileSavePicker();
            picker.FileTypeChoices.Add("M3U Playlists", new List<string> { ".m3u" });
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            var file = await picker.PickSaveFileAsync();
            using (StreamWriter writer = new StreamWriter(await file.OpenStreamForWriteAsync()))
            {
                writer.WriteLine("#EXTM3U");
                writer.WriteLine("");
                foreach (var track in songs)
                {
                    writer.WriteLine(string.Format("#EXTINF:{0},{1} - {2}", track.Length, track.LeadArtist, track.Title));
                    writer.WriteLine(track.Path);
                    writer.WriteLine("");
                }
            }
            return false;
        }
    }
}
