﻿/* 
	BreadPlayer. A music player made for Windows 10 store.
    Copyright (C) 2016  theweavrs (Abdullah Atta)

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.System;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using BreadPlayer.Common;
using BreadPlayer.Core;
using BreadPlayer.Core.Common;
using BreadPlayer.Core.Extensions;
using BreadPlayer.Core.Models;
using BreadPlayer.Database;
using BreadPlayer.Dialogs;
using BreadPlayer.Extensions;
using BreadPlayer.Helpers;
using BreadPlayer.Messengers;
using BreadPlayer.PlaylistBus;
using BreadPlayer.Themes;
using BreadPlayer.Services;
using BreadPlayer.Dispatcher;
using BreadPlayer.Models;
using BreadPlayer.SettingsViews;
using BreadPlayer.SettingsViews.ViewModels;
using System.Diagnostics;
using System.Collections;

namespace BreadPlayer.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        #region Properties
        public AccountsViewModel AccountSettingsVM { get; set; }
        public AudioSettingsViewModel AudioSettingsVM { get; set; }

        ThreadSafeObservableCollection<SettingGroup> settingsCollection;
        public ThreadSafeObservableCollection<SettingGroup> SettingsCollection
        {
            get => settingsCollection;
            set => Set(ref settingsCollection, value);
        }

        private bool _enableBlur;
        public bool EnableBlur
        {
            get => _enableBlur;
            set
            {
                Set(ref _enableBlur, value);
                SettingsHelper.SaveLocalSetting("EnableBlur", value);
            }
        }

        private bool _preventScreenFromLocking;
        public bool PreventScreenFromLocking
        {
            get => _preventScreenFromLocking;
            set
            {
                Set(ref _preventScreenFromLocking, value);
                if (value)
                {
                    KeepScreenActive();
                }
                else
                {
                    ReleaseDisplayRequest();
                }
            }
        }

        private bool _replaceLockscreenWithAlbumArt;
        public bool ReplaceLockscreenWithAlbumArt
        {
            get => _replaceLockscreenWithAlbumArt;
            set
            {
                Set(ref _replaceLockscreenWithAlbumArt, value);
                SettingsHelper.SaveLocalSetting("ReplaceLockscreenWithAlbumArt", value);
            }
        }

        private string _uiTextType;
        public string UiTextType
        {
            get => _uiTextType;
            set
            {
                Set(ref _uiTextType, value);
                SettingsHelper.SaveRoamingSetting("UITextType", _uiTextType);
            }
        }

        private bool _isThemeDark;
        public bool IsThemeDark
        {
            get => _isThemeDark;
            set
            {
                Set(ref _isThemeDark, value);
                SettingsHelper.SaveLocalSetting("SelectedTheme", _isThemeDark ? "Light" : "Dark");
                // SharedLogic.InitializeTheme();
            }
        }

        ThreadSafeObservableCollection<StorageFolder> _LibraryFoldersCollection;
        public ThreadSafeObservableCollection<StorageFolder> LibraryFoldersCollection
        {
            get
            {
                if (_LibraryFoldersCollection == null)
                {
                    _LibraryFoldersCollection = new ThreadSafeObservableCollection<StorageFolder>();
                }
                return _LibraryFoldersCollection;
            }
            set => Set(ref _LibraryFoldersCollection, value);
        }
        public static GroupedObservableCollection<IGroupKey, Mediafile> TracksCollection
        { get; set; }

        private string _timeClosed;
        public string TimeClosed
        {
            get => _timeClosed;
            set
            {
                Set(ref _timeClosed, value);
                SettingsHelper.SaveLocalSetting("timeclosed", _timeClosed);
            }
        }

        private string _timeOpened;
        public string TimeOpened
        {
            get => _timeOpened;
            set => Set(ref _timeOpened, value);
        }

        private List<StorageFile> _modifiedFiles = new List<StorageFile>();
        public List<StorageFile> ModifiedFiles
        {
            get => _modifiedFiles;
            set => Set(ref _modifiedFiles, value);
        }

        private bool _changeAccentByAlbumart;
        public bool ChangeAccentByAlbumArt
        {
            get => _changeAccentByAlbumart;
            set
            {
                Set(ref _changeAccentByAlbumart, value);
                if (value == false)
                {
                    ThemeManager.SetThemeColor(null);
                }
                else
                {
                    ThemeManager.SetThemeColor("default");
                }

                SettingsHelper.SaveRoamingSetting("ChangeAccentByAlbumArt", _changeAccentByAlbumart);
            }
        }

        private LibraryService LibraryService { get; set; }
        #endregion

        #region MessageHandling
        private async void HandleLibraryLoadedMessage(Message message)
        {
            if (message.Payload is List<object> list)
            {
                TracksCollection = list[0] as GroupedObservableCollection<IGroupKey, Mediafile>;
                if (LibraryService.SongCount <= 0)
                {
                    await AutoLoadMusicLibraryAsync().ConfigureAwait(false);
                }
                if (TracksCollection != null)
                {
                    message.HandledStatus = MessageHandledStatus.HandledContinue;
                }
            }
            Messenger.Instance.DeRegister(MessageTypes.MsgLibraryLoaded, new Action<Message>(HandleLibraryLoadedMessage));
        }
        #endregion

        private StorageLibraryService StorageLibraryService { get; set; }
        #region Ctor  
        public SettingsViewModel()
        {
            InitSettingsCollection();
            AccountSettingsVM = new AccountsViewModel();
            AudioSettingsVM = new AudioSettingsViewModel();

            LibraryService = new LibraryService(new DocumentStoreDatabaseService(SharedLogic.DatabasePath, "Tracks"));
            PropertyChanged += SettingsViewModel_PropertyChanged;
            _changeAccentByAlbumart = SettingsHelper.GetRoamingSetting<bool>("ChangeAccentByAlbumArt", true);
            _timeOpened = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _uiTextType = SettingsHelper.GetRoamingSetting<string>("UITextType", "Normal");
            _isThemeDark = SettingsHelper.GetLocalSetting<string>("SelectedTheme", "Light") == "Light" ? true : false;
            _enableBlur = SettingsHelper.GetLocalSetting<bool>("EnableBlur", !InitializeCore.IsMobile);
            _replaceLockscreenWithAlbumArt = SettingsHelper.GetLocalSetting<bool>("replaceLockscreenWithAlbumArt", false);
            _timeOpened = DateTime.Now.ToString();
            Messenger.Instance.Register(MessageTypes.MsgLibraryLoaded, new Action<Message>(HandleLibraryLoadedMessage));
            StorageLibraryService = new StorageLibraryService();
            StorageLibraryService.StorageItemsUpdated += StorageLibraryService_StorageItemsUpdated;
            LoadFolders();
        }

        public void InitSettingsCollection()
        {
            SettingsCollection = new ThreadSafeObservableCollection<SettingGroup>()
            {
                new SettingGroup("\uE771","Personlization","Lockscreen, font, theme", typeof(PersonlizationView)),
                new SettingGroup("\uE8D6","Music Library","Folder import, playlists", typeof(MusicLibrarySettingsView)),
                new SettingGroup("\uE910","Accounts","Last.fm, lyrics", typeof(AccountsView)),
                new SettingGroup("\uE144", "Keyboard Bindings", "Keyboard shortcuts", typeof(KeyboardSettingsView)),
                new SettingGroup("\uE770", "Core", "Reset, notifications, lock screen", typeof(CoreSettingsView)),
                new SettingGroup("\uE7F6", "Audio", "Equalizer, volume, cross-fade", typeof(AudioSettingsView)),
                new SettingGroup("\uE779", "Contact", "Facebook, email, github", typeof(ContactView)),
                new SettingGroup("\uE946", "About", "Version info, license, credits", typeof(AboutView)),
                new SettingGroup("\uE789", "Contribute", "Translation, bug hunting, coding", typeof(ContributeView)),
            };
        }
        #endregion

        #region Commands

        #region Definitions   
        
        private DelegateCommand _loadCommand;
        /// <summary>
        /// Gets load library command. This calls the <see cref="Load"/> method.
        /// </summary>
        public DelegateCommand LoadCommand { get { if (_loadCommand == null) { _loadCommand = new DelegateCommand(Load); } return _loadCommand; } }

        private DelegateCommand _importPlaylistCommand;
        /// <summary>
        /// Gets load library command. This calls the <see cref="Load"/> method.
        /// </summary>
        public DelegateCommand ImportPlaylistCommand { get { if (_importPlaylistCommand == null) { _importPlaylistCommand = new DelegateCommand(ImportPlaylists); } return _importPlaylistCommand; } }

        private DelegateCommand _resetCommand;
        /// <summary>
        /// Gets load library command. This calls the <see cref="Load"/> method.
        /// </summary>
        public DelegateCommand ResetCommand { get { if (_resetCommand == null) { _resetCommand = new DelegateCommand(Reset); } return _resetCommand; } }

        #endregion

        #region Implementation      
        private async void Reset()
        {
            try
            {
                Messenger.Instance.NotifyColleagues(MessageTypes.MsgDispose);
                LibraryFoldersCollection.Clear();
                await ApplicationData.Current.ClearAsync();
                ResetCommand.IsEnabled = false;
                await Task.Delay(200);
                ResetCommand.IsEnabled = true;
                LibraryService = new LibraryService(new DocumentStoreDatabaseService(SharedLogic.DatabasePath, "Tracks"));
            }
            catch (Exception ex)
            {
                BLogger.Logger.Error("Error occured while resetting the player.", ex);
            }
        }
        private async void ImportPlaylists()
        {
            FileOpenPicker openPicker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.MusicLibrary
            };
            openPicker.FileTypeFilter.Add(".m3u");
            openPicker.FileTypeFilter.Add(".pls");
            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                StorageApplicationPermissions.FutureAccessList.Add(file);

                IPlaylist playlist = null;
                if (Path.GetExtension(file.Path) == ".m3u")
                {
                    playlist = new M3U();
                }
                else
                {
                    playlist = new Pls();
                }

                var plist = new Playlist { Name = file.DisplayName, IsExternal = true, Path = file.Path};
                Messenger.Instance.NotifyColleagues(MessageTypes.MsgAddPlaylist, plist);
            }
        }
        /// <summary>
        /// Loads songs from a specified folder into the library. <seealso cref="LoadCommand"/>
        /// </summary>
        public async void Load()
        {
            try
            {
                FolderPicker picker = new FolderPicker();
                picker.FileTypeFilter.Add(".mp3");
                picker.FileTypeFilter.Add(".wav");
                picker.FileTypeFilter.Add(".ogg");
                picker.FileTypeFilter.Add(".flac");
                picker.FileTypeFilter.Add(".m4a");
                picker.FileTypeFilter.Add(".aif");
                picker.FileTypeFilter.Add(".wma");
                picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
                picker.ViewMode = PickerViewMode.List;
                picker.CommitButtonText = "Import";
                StorageFolder folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    StorageApplicationPermissions.FutureAccessList.Add(folder);
                    await LoadFolderAsync(folder);
                }
            }
            catch (UnauthorizedAccessException)
            {
                await NotificationManager.ShowMessageAsync("You are not authorized to access this folder. Please choose another folder or try again.");
            }
        }

        #endregion

        #endregion

        #region Methods

        #region General Settings Methods

        private DisplayRequest _displayRequest;
        private void KeepScreenActive()
        {
            if (_displayRequest == null)
            {
                _displayRequest = new DisplayRequest();
                // This call activates a display-required request. If successful,  
                // the screen is guaranteed not to turn off automatically due to user inactivity. 
                _displayRequest.RequestActive();
            }
        }
        private void ReleaseDisplayRequest()
        {
            // This call de-activates the display-required request. If successful, the screen 
            // might be turned off automatically due to a user inactivity, depending on the 
            // power policy settings of the system. The requestRelease method throws an exception  
            // if it is called before a successful requestActive call on this object. 
            if (_displayRequest != null)
            {
                _displayRequest.RequestRelease();
                _displayRequest = null;
            }
        }

        #endregion

        #region LoadFoldersCommand
        public async Task LoadFolders()
        {
            if (LibraryFoldersCollection.Count <= 0)
            {
                var folderPaths = SettingsHelper.GetLocalSetting<string>("folders", null);
                if (folderPaths != null)
                {
                    foreach (var folder in folderPaths.Split('|'))
                    {
                        if (!string.IsNullOrEmpty(folder))
                        {
                            try
                            {
                                var storageFolder = await StorageFolder.GetFolderFromPathAsync(folder);
                                LibraryFoldersCollection.Add(storageFolder);
                            }
                            catch (FileNotFoundException)
                            { }
                            catch (UnauthorizedAccessException)
                            { }
                        }
                    }
                }
            }
        }
        #endregion

        #region Add Methods        
        /// <summary>
        /// Adds storage files into library.
        /// </summary>
        /// <param name="files">List containing StorageFile(s).</param>
        /// <returns></returns>
        public async static Task AddStorageFilesToLibraryAsync(IEnumerable<StorageFile> files)
        {
            foreach (var file in files)
            {
                Mediafile mp3File = null;
                int index = -1;
                if (file != null)
                {
                    if (TracksCollection.Elements.Any(t => t.Path == file.Path))
                    {
                        index = TracksCollection.Elements.IndexOf(TracksCollection.Elements.First(t => t.Path == file.Path));
                        SharedLogic.RemoveMediafile(TracksCollection.Elements.First(t => t.Path == file.Path));
                    }
                    //this methods notifies the Player that one song is loaded. We use both 'count' and 'i' variable here to report current progress.
                    await SharedLogic.NotificationManager.ShowMessageAsync(" Song(s) Loaded");
                    await Task.Run(async () =>
                    {
                        //here we load into 'mp3file' variable our processed Song. This is a long process, loading all the properties and the album art.
                        mp3File = await TagReaderHelper.CreateMediafile(file, false); //the core of the whole method.
                        await SaveSingleFileAlbumArtAsync(mp3File, file).ConfigureAwait(false);
                    });
                    SharedLogic.AddMediafile(mp3File, index);
                }
            }
        }
        #endregion

        #region Load Methods
        private async Task LoadFolderAsync(StorageFolder folder)
        {
            if (folder == null)
                return;
            Messenger.Instance.NotifyColleagues(MessageTypes.MsgUpdateSongCount, (short)2);
            LibraryFoldersCollection.Add(folder);
            await AddFolderToLibraryAsync(folder);
        }

        /// <summary>
        /// Auto loads the User's Music Libary on first load.
        /// </summary>
        private async Task AutoLoadMusicLibraryAsync()
        {
            try
            {
                if(KnownFolders.MusicLibrary != null)
                    await LoadFolderAsync(KnownFolders.MusicLibrary);                
            }
            catch (Exception ex)
            {
                BLogger.Logger.Error("Auto Loading of library failed.", ex);
                await NotificationManager.ShowMessageAsync(ex.Message);
            }
        }
        /// <summary>
        /// Add folder to Library asynchronously.
        /// </summary>
        /// <param name="queryResult">The query result after querying in a specific folder.</param>
        /// <returns></returns>
        public async Task AddFolderToLibraryAsync(StorageFolder folder)
        {
            StorageFileQueryResult queryResult = folder.CreateFileQueryWithOptions(DirectoryWalker.GetQueryOptions());
            Stopwatch watch = Stopwatch.StartNew();
            uint index = 0, stepSize = 20;
            IReadOnlyList<StorageFile> files = await queryResult.GetFilesAsync(index, stepSize);
            index += stepSize;
            //this is a temporary list to collect all the processed Mediafiles. We use List because it is fast. Faster than using ObservableCollection directly because of the events firing on every add.


            int failedCount = 0;
            var count = await queryResult.GetItemCountAsync();
            var tempList = new List<Mediafile>((int)count);
            short progress = 0;
            await BreadDispatcher.InvokeAsync(async () =>
            {
                try
                {
                    // Note that I'm paging in the files as described
                    while (files.Count != 0)
                    {
                        var fileTask = queryResult.GetFilesAsync(index, stepSize).AsTask();
                        for (int i = 0; i < files.Count; i++)
                        {
                            progress++;
                            try
                            {
                                Messenger.Instance.NotifyColleagues(MessageTypes.MsgUpdateSongCount, progress);
                                Mediafile mp3File = await TagReaderHelper.CreateMediafile(files[i], false).ConfigureAwait(false); //the core of the whole method.
                                mp3File.FolderPath = Path.GetDirectoryName(files[i].Path);
                                await SaveSingleFileAlbumArtAsync(mp3File, files[i]).ConfigureAwait(false);

                                await NotificationManager.ShowMessageAsync(progress + "\\" + count + " Song(s) Loaded", 0);

                                tempList.Add(mp3File);
                            }
                            catch (Exception ex)
                            {
                                BLogger.Logger.Error("Loading of a song in folder failed.", ex);
                                //we catch and report any exception without distrubing the 'foreach flow'.
                                await NotificationManager.ShowMessageAsync(ex.Message + " || Occured on: " + files[i].Path);
                                failedCount++;
                            }
                        }
                        files = await fileTask;
                        index += stepSize;
                    }
                }
                catch (Exception ex)
                {
                    string message1 = ex.Message + "||" + ex.InnerException;
                    await NotificationManager.ShowMessageAsync(message1);
                }
                watch.Stop();
                BLogger.Logger.Info("Time to run: " + watch.ElapsedMilliseconds + " ms");
                Debug.WriteLine("Time to run: " + watch.ElapsedMilliseconds + " ms");
                var uniqueFiles = tempList.DistinctBy(f => f.OrginalFilename).ToList();
                Messenger.Instance.NotifyColleagues(MessageTypes.MsgUpdateSongCount, uniqueFiles.Count);
                await NotificationManager.ShowMessageAsync("Adding songs into library. Please wait...");
                await TracksCollection.AddRange(uniqueFiles).ConfigureAwait(false);
                await NotificationManager.ShowMessageAsync("Saving songs into database. Please wait...");

                await LibraryService.AddMediafiles(uniqueFiles);
                Messenger.Instance.NotifyColleagues(MessageTypes.MsgUpdateSongCount, "Done!");
                Messenger.Instance.NotifyColleagues(MessageTypes.MsgAddAlbums, uniqueFiles);

                string message = string.Format("Songs successfully imported! Total Songs: {0}; Failed: {1}; Loaded: {2}", count, failedCount, uniqueFiles.Count);

                BLogger.Logger.Info(message);
                await NotificationManager.ShowMessageAsync(message);
                tempList.Clear();
            });
        }

        #endregion

        #region AlbumArt Methods
        public static async Task SaveSingleFileAlbumArtAsync(Mediafile mp3File, StorageFile file = null)
        {
            if (mp3File == null || mp3File.Path == null) return;

            try
            {
                if (file == null)
                {
                    file = await StorageFile.GetFileFromPathAsync(mp3File.Path);
                }

                var albumartFolder = ApplicationData.Current.LocalFolder;
                var albumartLocation = albumartFolder.Path + @"\AlbumArts\" + (mp3File.Album + mp3File.LeadArtist).ToLower().ToSha1() + ".jpg";

                if (!SharedLogic.VerifyFileExists(albumartLocation, 300))
                {
                    bool albumSaved = await TagReaderHelper.SaveAlbumArtsAsync(file, mp3File);
                    mp3File.AttachedPicture = albumSaved ? albumartLocation : null;
                }
                file = null;
            }
            catch (Exception ex)
            {
                BLogger.Logger.Info("Failed to save albumart.", ex);
                await SharedLogic.NotificationManager.ShowMessageAsync("Failed to save album art of " + mp3File.OrginalFilename);
            }
        }

        private static async Task SaveMultipleAlbumArtsAsync(IEnumerable<Mediafile> files)
        {
            foreach (var file in files)
            {
                await SaveSingleFileAlbumArtAsync(file);
            }
        }
        #endregion


        #endregion

        #region Events
        private async void SettingsViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ReplaceLockscreenWithAlbumArt")
            {
                if (ReplaceLockscreenWithAlbumArt)
                {
                    _replaceLockscreenWithAlbumArt = await LockscreenHelper.SaveCurrentLockscreenImage();
                }
                else
                {
                    await LockscreenHelper.ResetLockscreenImage();
                }
            }
        }

        private async void StorageLibraryService_StorageItemsUpdated(object sender, StorageItemsUpdatedEventArgs e)
        {  
            //tell the reader that we accept the changes
            //so that the same files are not served to us again.
            await (sender as StorageLibraryChangeTracker).GetChangeReader().AcceptChangesAsync();

            //check if there are any updated items.
            if (e.UpdatedItems != null && e.UpdatedItems.Any())
            {
              
                foreach (var item in e.UpdatedItems)
                {
                    //sometimes, the breadplayer log is also included in updated files,
                    //this is to avoid accidently causing a crash.
                    if (Path.GetExtension(item.Path) == ".log")
                    {
                        return;
                    }
                    switch (item.ChangeType)
                    {
                        //file has been created
                        //NOTE: sometimes a altered file also has this ChangeType, so we check for both.
                        case StorageLibraryChangeType.Created:
                            await item.UpdateChangedItem(TracksCollection.Elements, LibraryService);
                            break;
                        //file has been deleted.
                        //NOTE: ChangeType for a file replaced is also this.
                        //TODO: Add logic for a file that is replaced.
                        case StorageLibraryChangeType.Deleted:
                            await item.RemoveItem(TracksCollection.Elements, LibraryService);
                            break;
                            //file was moved or renamed.
                            //NOTE: Logic for moved is the same as renamed (i.e. the path is changed.)
                            case StorageLibraryChangeType.MovedOrRenamed:
                            if (item.IsOfType(StorageItemTypes.File))
                            {
                                if (item.IsItemInLibrary(TracksCollection.Elements, out Mediafile renamedItem))
                                {
                                    renamedItem.Path = item.Path;
                                    if(await LibraryService.UpdateMediafile(renamedItem))
                                    {
                                        await SharedLogic.NotificationManager.ShowMessageAsync(string.Format("Mediafile Updated. File Path: {0}", renamedItem.Path), 5);
                                    }
                                }
                            }
                            else
                            {
                                await item.RenameFolder(TracksCollection.Elements, LibraryService);
                            }
                            break;
                            //this is almost never invoked but just in case,
                            //we implement RemoveItem logic here.
                        case StorageLibraryChangeType.MovedOutOfLibrary:
                            await item.RemoveItem(TracksCollection.Elements, LibraryService);
                            break;
                            //this is also never invoked but just in case,
                            //we implement AddNewItem logic here.
                        case StorageLibraryChangeType.MovedIntoLibrary:
                            await item.AddNewItem();
                            break;
                            //file's content was changed in some manner. Can be a tag change.
                        case StorageLibraryChangeType.ContentsChanged:
                            await item.UpdateChangedItem(TracksCollection.Elements, LibraryService);
                            break;
                            //TODO: Find a way to invoke this and then implement logic accordingly.
                        case StorageLibraryChangeType.ContentsReplaced:
                            break;
                            //Change was lost. According to the docs, we should Reset the Tracker here.
                        case StorageLibraryChangeType.ChangeTrackingLost:
                            (sender as StorageLibraryChangeTracker).Reset();
                            break;
                    }
                }
            }
        }
        #endregion

    }
}

