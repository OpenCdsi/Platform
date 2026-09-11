/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using OpenCdsi.Mobile.ViewModels;

namespace OpenCdsi.Mobile.Views;

public partial class ChapterDetailPage : ContentPage
{
    private readonly ChapterDetailViewModel _viewModel;

    public ChapterDetailPage(ChapterDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // MVVM has no bindable CollectionView.ScrollTo - this is a deliberate, narrow code-behind
        // exception (same category as other scroll/anchor cases in this app) purely to carry out
        // the auto-jump-to-Contraindications behavior the ViewModel decides on.
        _viewModel.ScrollToSectionRequested += OnScrollToSectionRequested;
    }

    private void OnScrollToSectionRequested(object? sender, int index)
        => SectionsCollectionView.ScrollTo(index, position: ScrollToPosition.Start, animate: false);
}
