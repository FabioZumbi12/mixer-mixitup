﻿using MixItUp.Base.Model.Actions;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Actions
{
    public class TextToSpeechActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.TextToSpeech; } }

        public IEnumerable<TextToSpeechProviderType> ProviderTypes { get; private set; } = EnumHelper.GetEnumList<TextToSpeechProviderType>();

        public TextToSpeechProviderType SelectedProviderType
        {
            get { return this.selectedProviderType; }
            set
            {
                this.selectedProviderType = value;
                this.NotifyPropertyChanged();
                this.UpdateTextToSpeechProvider();
            }
        }
        private TextToSpeechProviderType selectedProviderType = TextToSpeechProviderType.WindowsTextToSpeech;

        public bool OverlayNotEnabled { get { return this.SelectedProviderType == TextToSpeechProviderType.WindowsTextToSpeech && !ServiceManager.Get<OverlayV3Service>().IsConnected; } }

        public ThreadSafeObservableCollection<TextToSpeechVoice> Voices { get; private set; } = new ThreadSafeObservableCollection<TextToSpeechVoice>();

        public TextToSpeechVoice SelectedVoice
        {
            get { return this.selectedVoice; }
            set
            {
                this.selectedVoice = value;
                this.NotifyPropertyChanged();
            }
        }
        private TextToSpeechVoice selectedVoice;

        public int VolumeMinimum { get; private set; }
        public int VolumeMaximum { get; private set; }
        public bool VolumeChangable { get { return this.VolumeMinimum != this.VolumeMaximum; } }
        public int Volume
        {
            get { return this.volume; }
            set
            {
                this.volume = MathHelper.Clamp(value, this.VolumeMinimum, this.VolumeMaximum);
                this.NotifyPropertyChanged();
            }
        }
        private int volume = 0;

        public int PitchMinimum { get; private set; }
        public int PitchMaximum { get; private set; }
        public bool PitchChangable { get { return this.PitchMinimum != this.PitchMaximum; } }
        public int Pitch
        {
            get { return this.pitch; }
            set
            {
                this.pitch = MathHelper.Clamp(value, this.PitchMinimum, this.PitchMaximum);
                this.NotifyPropertyChanged();
            }
        }
        private int pitch = 0;

        public int RateMinimum { get; private set; }
        public int RateMaximum { get; private set; }
        public bool RateChangable { get { return this.RateMinimum != this.RateMaximum; } }
        public int Rate
        {
            get { return this.rate; }
            set
            {
                this.rate = MathHelper.Clamp(value, this.RateMinimum, this.RateMaximum);
                this.NotifyPropertyChanged();
            }
        }
        private int rate = 0;

        public string Text
        {
            get { return this.text; }
            set
            {
                this.text = value;
                this.NotifyPropertyChanged();
            }
        }
        private string text;

        public bool WaitForFinish
        {
            get { return this.waitForFinish; }
            set
            {
                this.waitForFinish = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool waitForFinish;

        public TextToSpeechActionEditorControlViewModel(TextToSpeechActionModel action)
            : base(action)
        {
            this.selectedProviderType = action.ProviderType;
            this.SelectedVoice = this.Voices.FirstOrDefault(v => string.Equals(v.ID, action.Voice, StringComparison.OrdinalIgnoreCase));
            this.Text = action.Text;
            this.Volume = action.Volume;
            this.Pitch = action.Pitch;
            this.Rate = action.Rate;
            this.WaitForFinish = action.WaitForFinish;
        }

        public TextToSpeechActionEditorControlViewModel()
            : base()
        {
            this.UpdateTextToSpeechProvider();
        }

        public override Task<Result> Validate()
        {
            if (this.SelectedVoice == null)
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.TextToSpeechActionMissingVoice));
            }

            if (string.IsNullOrEmpty(this.Text))
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.TextToSpeechActionMissingText));
            }

            if (this.Volume < this.VolumeMinimum || this.Volume > this.VolumeMaximum)
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.TextToSpeechActionInvalidVolume));
            }

            if (this.Pitch < this.PitchMinimum || this.Pitch > this.PitchMaximum)
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.TextToSpeechActionInvalidPitch));
            }

            if (this.Rate < this.RateMinimum || this.Rate > this.RateMaximum)
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.TextToSpeechActionInvalidRate));
            }

            return Task.FromResult(new Result());
        }

        protected override Task<ActionModelBase> GetActionInternal() { return Task.FromResult<ActionModelBase>(new TextToSpeechActionModel(this.SelectedProviderType, this.Text, this.SelectedVoice.ID, this.Volume, this.Pitch, this.Rate, this.WaitForFinish)); }

        private void UpdateTextToSpeechProvider()
        {
            foreach (ITextToSpeechService service in ServiceManager.GetAll<ITextToSpeechService>())
            {
                if (service.ProviderType == this.SelectedProviderType)
                {
                    this.NotifyPropertyChanged(nameof(this.OverlayNotEnabled));

                    string voiceID = (this.SelectedVoice != null) ? this.SelectedVoice.ID : null;
                    this.Voices.ClearAndAddRange(service.GetVoices());
                    if (voiceID != null)
                    {
                        this.SelectedVoice = this.Voices.FirstOrDefault(v => string.Equals(v.ID, voiceID, StringComparison.OrdinalIgnoreCase));
                    }

                    this.VolumeMinimum = service.VolumeMinimum;
                    this.VolumeMaximum = service.VolumeMaximum;
                    this.Volume = this.VolumeChangable ? service.VolumeDefault : 0;
                    this.NotifyPropertyChanged(nameof(this.VolumeMinimum));
                    this.NotifyPropertyChanged(nameof(this.VolumeMaximum));
                    this.NotifyPropertyChanged(nameof(this.VolumeChangable));
                    this.NotifyPropertyChanged(nameof(this.Volume));

                    this.PitchMinimum = service.PitchMinimum;
                    this.PitchMaximum = service.PitchMaximum;
                    this.Pitch = this.PitchChangable ? service.PitchDefault : 0;
                    this.NotifyPropertyChanged(nameof(this.PitchMinimum));
                    this.NotifyPropertyChanged(nameof(this.PitchMaximum));
                    this.NotifyPropertyChanged(nameof(this.PitchChangable));
                    this.NotifyPropertyChanged(nameof(this.Pitch));

                    this.RateMinimum = service.RateMinimum;
                    this.RateMaximum = service.RateMaximum;
                    this.RateMinimum = this.RateChangable ? service.RateDefault : 0;
                    this.NotifyPropertyChanged(nameof(this.RateMinimum));
                    this.NotifyPropertyChanged(nameof(this.RateMaximum));
                    this.NotifyPropertyChanged(nameof(this.RateChangable));
                    this.NotifyPropertyChanged(nameof(this.Rate));
                    break;
                }
            }
        }
    }
}
