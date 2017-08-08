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

using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using BreadPlayer.Core.Models;
using BreadPlayer.ViewModels;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace BreadPlayer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LibraryView
    {
        public LibraryView()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }
        
        private void fileBox_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Add file/folder(s) to library";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }

        private void semanticZoom_ViewChangeStarted(object sender, SemanticZoomViewChangedEventArgs e)
        {
            if (e.IsSourceZoomedInView)
            {
                (this.FindName("alphabetList") as GridView).Visibility = Visibility.Visible;
                return;
            }
            try
            { 
                // get the selected group
                var selectedGroup = e.SourceItem.Item as string;
                Grouping<string, Mediafile> myGroup = (DataContext as LibraryViewModel).TracksCollection.FirstOrDefault(g => g.Key.StartsWith(selectedGroup));
                backBtn.Visibility = Visibility.Collapsed;
                e.DestinationItem = new SemanticZoomLocation
                {
                    Bounds = new Rect(0, 0, 1, 1),
                    Item = myGroup
                };
            }
            catch { }
        }
        
    }
}
