﻿using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Overlay;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Overlay;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Actions
{
    public enum OverlayActionTypeEnum
    {
        Text,
        Image,
        Video,
        YouTube,
        HTML,
        WebPage,
    }

    [Obsolete]
    public class OverlayActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.Overlay; } }

        public IEnumerable<OverlayActionTypeEnum> ActionTypes { get { return EnumHelper.GetEnumList<OverlayActionTypeEnum>(); } }

        public OverlayActionTypeEnum SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("OverlayNotEnabled");
                this.NotifyPropertyChanged("OverlayEnabled");
                this.NotifyPropertyChanged("ShowItemProperties");
                this.NotifyPropertyChanged("ShowTextItem");
                this.NotifyPropertyChanged("ShowImageItem");
                this.NotifyPropertyChanged("ShowVideoItem");
                this.NotifyPropertyChanged("ShowYouTubeItem");
                this.NotifyPropertyChanged("ShowHTMLItem");
                this.NotifyPropertyChanged("ShowWebPageItem");
            }
        }
        private OverlayActionTypeEnum selectedActionType;

        public bool OverlayNotEnabled { get { return !ServiceManager.Get<OverlayService>().IsConnected; } }

        public bool OverlayEnabled { get { return !this.OverlayNotEnabled; } }

        public IEnumerable<string> OverlayEndpoints { get { return ServiceManager.Get<OverlayService>().GetOverlayNames(); } }

        public string SelectedOverlayEndpoint
        {
            get { return this.selectedOverlayEndpoint; }
            set
            {
                var overlays = ServiceManager.Get<OverlayService>().GetOverlayNames();
                if (overlays.Contains(value))
                {
                    this.selectedOverlayEndpoint = value;
                }
                else
                {
                    this.selectedOverlayEndpoint = ServiceManager.Get<OverlayService>().DefaultOverlayName;
                }
                this.NotifyPropertyChanged();
            }
        }
        private string selectedOverlayEndpoint;

        public bool ShowItemProperties { get { return this.ShowTextItem || this.ShowImageItem || this.ShowVideoItem || this.ShowYouTubeItem || this.ShowHTMLItem || this.ShowWebPageItem; } }

        public bool ShowTextItem { get { return this.SelectedActionType == OverlayActionTypeEnum.Text; } }

        public OverlayTextItemV3ViewModel TextItemViewModel
        {
            get { return this.textItemViewModel; }
            set
            {
                this.textItemViewModel = value;
                this.NotifyPropertyChanged();
            }
        }
        private OverlayTextItemV3ViewModel textItemViewModel = new OverlayTextItemV3ViewModel();

        public bool ShowImageItem { get { return this.SelectedActionType == OverlayActionTypeEnum.Image; } }

        public OverlayImageItemV3ViewModel ImageItemViewModel
        {
            get { return this.imageItemViewModel; }
            set
            {
                this.imageItemViewModel = value;
                this.NotifyPropertyChanged();
            }
        }
        private OverlayImageItemV3ViewModel imageItemViewModel = new OverlayImageItemV3ViewModel();

        public bool ShowVideoItem { get { return this.SelectedActionType == OverlayActionTypeEnum.Video; } }

        public OverlayVideoItemV3ViewModel VideoItemViewModel
        {
            get { return this.videoItemViewModel; }
            set
            {
                this.videoItemViewModel = value;
                this.NotifyPropertyChanged();
            }
        }
        private OverlayVideoItemV3ViewModel videoItemViewModel = new OverlayVideoItemV3ViewModel();

        public bool ShowYouTubeItem { get { return this.SelectedActionType == OverlayActionTypeEnum.YouTube; } }

        public OverlayYouTubeItemV3ViewModel YouTubeItemViewModel
        {
            get { return this.youTubeItemViewModel; }
            set
            {
                this.youTubeItemViewModel = value;
                this.NotifyPropertyChanged();
            }
        }
        private OverlayYouTubeItemV3ViewModel youTubeItemViewModel = new OverlayYouTubeItemV3ViewModel();

        public bool ShowHTMLItem { get { return this.SelectedActionType == OverlayActionTypeEnum.HTML; } }

        public OverlayHTMLItemV3ViewModel HTMLItemViewModel
        {
            get { return this.htmlItemViewModel; }
            set
            {
                this.htmlItemViewModel = value;
                this.NotifyPropertyChanged();
            }
        }
        private OverlayHTMLItemV3ViewModel htmlItemViewModel = new OverlayHTMLItemV3ViewModel();

        public bool ShowWebPageItem { get { return this.SelectedActionType == OverlayActionTypeEnum.WebPage; } }

        public OverlayWebPageItemV3ViewModel WebPageItemViewModel
        {
            get { return this.webPageItemViewModel; }
            set
            {
                this.webPageItemViewModel = value;
                this.NotifyPropertyChanged();
            }
        }
        private OverlayWebPageItemV3ViewModel webPageItemViewModel = new OverlayWebPageItemV3ViewModel();

        public OverlayItemPositionV3ViewModel ItemPosition
        {
            get { return this.itemPosition; }
            set
            {
                this.itemPosition = value;
                this.NotifyPropertyChanged();
            }
        }
        private OverlayItemPositionV3ViewModel itemPosition = new OverlayItemPositionV3ViewModel();

        public string ItemDuration
        {
            get { return this.itemDuration; }
            set
            {
                this.itemDuration = value;
                this.NotifyPropertyChanged();
            }
        }
        private string itemDuration;

        public IEnumerable<OverlayItemEffectEntranceAnimationTypeEnum> EntranceAnimations { get { return EnumHelper.GetEnumList<OverlayItemEffectEntranceAnimationTypeEnum>(); } }

        public OverlayItemEffectEntranceAnimationTypeEnum SelectedEntranceAnimation
        {
            get { return this.selectedEntranceAnimation; }
            set
            {
                this.selectedEntranceAnimation = value;
                this.NotifyPropertyChanged();
            }
        }
        private OverlayItemEffectEntranceAnimationTypeEnum selectedEntranceAnimation;

        public IEnumerable<OverlayItemEffectExitAnimationTypeEnum> ExitAnimations { get { return EnumHelper.GetEnumList<OverlayItemEffectExitAnimationTypeEnum>(); } }

        public OverlayItemEffectExitAnimationTypeEnum SelectedExitAnimation
        {
            get { return this.selectedExitAnimation; }
            set
            {
                this.selectedExitAnimation = value;
                this.NotifyPropertyChanged();
            }
        }
        private OverlayItemEffectExitAnimationTypeEnum selectedExitAnimation;

        public IEnumerable<OverlayItemEffectVisibleAnimationTypeEnum> VisibleAnimations { get { return EnumHelper.GetEnumList<OverlayItemEffectVisibleAnimationTypeEnum>(); } }

        public OverlayItemEffectVisibleAnimationTypeEnum SelectedVisibleAnimation
        {
            get { return this.selectedVisibleAnimation; }
            set
            {
                this.selectedVisibleAnimation = value;
                this.NotifyPropertyChanged();
            }
        }
        private OverlayItemEffectVisibleAnimationTypeEnum selectedVisibleAnimation;

        public OverlayActionEditorControlViewModel(OverlayActionModel action)
            : base(action)
        {
            if (!string.IsNullOrEmpty(action.OverlayName))
            {
                this.SelectedOverlayEndpoint = action.OverlayName;
            }
            else
            {
                this.SelectedOverlayEndpoint = ServiceManager.Get<OverlayService>().DefaultOverlayName;
            }

            if (action.OverlayItemV3 != null)
            {
                this.ItemPosition = new OverlayItemPositionV3ViewModel(action.OverlayItemV3);

                //if (action.OverlayItem.Effects != null)
                //{
                //    this.ItemDuration = action.OverlayItem.Effects.Duration;
                //    this.SelectedEntranceAnimation = action.OverlayItem.Effects.EntranceAnimation;
                //    this.SelectedVisibleAnimation = action.OverlayItem.Effects.VisibleAnimation;
                //    this.SelectedExitAnimation = action.OverlayItem.Effects.ExitAnimation;
                //}

                if (action.OverlayItemV3.Type == OverlayItemV3Type.Text)
                {
                    this.SelectedActionType = OverlayActionTypeEnum.Text;
                    this.TextItemViewModel = new OverlayTextItemV3ViewModel((OverlayTextItemV3Model)action.OverlayItemV3);
                }
                else if (action.OverlayItemV3.Type == OverlayItemV3Type.Image)
                {
                    this.SelectedActionType = OverlayActionTypeEnum.Image;
                    this.ImageItemViewModel = new OverlayImageItemV3ViewModel((OverlayImageItemV3Model)action.OverlayItemV3);
                }
                else if (action.OverlayItemV3.Type == OverlayItemV3Type.Video)
                {
                    this.SelectedActionType = OverlayActionTypeEnum.Video;
                    this.VideoItemViewModel = new OverlayVideoItemV3ViewModel((OverlayVideoItemV3Model)action.OverlayItemV3);
                }
                else if (action.OverlayItemV3.Type == OverlayItemV3Type.YouTube)
                {
                    this.SelectedActionType = OverlayActionTypeEnum.YouTube;
                    this.YouTubeItemViewModel = new OverlayYouTubeItemV3ViewModel((OverlayYouTubeItemV3Model)action.OverlayItemV3);
                }
                else if (action.OverlayItemV3.Type == OverlayItemV3Type.HTML)
                {
                    this.SelectedActionType = OverlayActionTypeEnum.HTML;
                    this.HTMLItemViewModel = new OverlayHTMLItemV3ViewModel((OverlayHTMLItemV3Model)action.OverlayItemV3);
                }
                else if (action.OverlayItemV3.Type == OverlayItemV3Type.WebPage)
                {
                    this.SelectedActionType = OverlayActionTypeEnum.WebPage;
                    this.WebPageItemViewModel = new OverlayWebPageItemV3ViewModel((OverlayWebPageItemV3Model)action.OverlayItemV3);
                }
            }
        }

        public OverlayActionEditorControlViewModel()
            : base()
        {
            this.SelectedOverlayEndpoint = ServiceManager.Get<OverlayService>().DefaultOverlayName;
        }

        public override Task<Result> Validate()
        {
            //if (this.SelectedActionType == OverlayActionTypeEnum.ShowHideWidget)
            //{
            //    //if (this.SelectedWidget == null)
            //    //{
            //    //    return Task.FromResult(new Result(MixItUp.Base.Resources.OverlayActionMissingWidget));
            //    //}
            //}
            //else
            //{
            //    //if (this.ItemDuration <= 0)
            //    //{
            //    //    return Task.FromResult(new Result(MixItUp.Base.Resources.OverlayActionDurationInvalid));
            //    //}

            //    //if (this.SelectedItemViewModel == null)
            //    //{
            //    //    return Task.FromResult(new Result(MixItUp.Base.Resources.OverlayActionItemInvalid));
            //    //}

            //    //OverlayItemModelBase overlayItem = this.SelectedItemViewModel.GetOverlayItem();
            //    //if (overlayItem == null)
            //    //{
            //    //    return Task.FromResult(new Result(MixItUp.Base.Resources.OverlayActionItemInvalid));
            //    //}
            //}
            return Task.FromResult(new Result());
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            if (this.ShowItemProperties)
            {
                OverlayItemV3ModelBase item = null;
                if (this.SelectedActionType == OverlayActionTypeEnum.Text)
                {
                    item = this.TextItemViewModel.GetItem();
                }
                else if (this.SelectedActionType == OverlayActionTypeEnum.Image)
                {
                    item = this.ImageItemViewModel.GetItem();
                }
                else if (this.SelectedActionType == OverlayActionTypeEnum.Video)
                {
                    item = this.VideoItemViewModel.GetItem();
                }
                else if (this.SelectedActionType == OverlayActionTypeEnum.YouTube)
                {
                    item = this.YouTubeItemViewModel.GetItem();
                }
                else if (this.SelectedActionType == OverlayActionTypeEnum.HTML)
                {
                    item = this.HTMLItemViewModel.GetItem();
                }
                else if (this.SelectedActionType == OverlayActionTypeEnum.WebPage)
                {
                    item = this.WebPageItemViewModel.GetItem();
                }

                if (item != null)
                {
                    this.ItemPosition.SetPosition(item);

                    return Task.FromResult<ActionModelBase>(new OverlayActionModel(this.SelectedOverlayEndpoint, item));
                }
            }
            return Task.FromResult<ActionModelBase>(null);
        }
    }
}
