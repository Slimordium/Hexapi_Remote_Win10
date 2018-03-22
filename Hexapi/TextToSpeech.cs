using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace Hexapi.Service
{
    public class TextToSpeech
    {
        public ISubject<string> TextSubject { get; } = new Subject<string>();

        private readonly MediaElement _mediaElement = new MediaElement();

        private IDisposable _disposable;

        public TextToSpeech()
        {
            _disposable = TextSubject.ObserveOnDispatcher().Subscribe(async text => { await Speak(text); });
        }

        private async Task Speak(string text)
        {
            using (var speech = new SpeechSynthesizer())
            {
                speech.Voice = SpeechSynthesizer.AllVoices.First(gender => gender.Gender == VoiceGender.Female);

                var stream = await speech.SynthesizeTextToStreamAsync(text);
                _mediaElement.SetSource(stream, stream.ContentType);
                _mediaElement.Play();
            }
        }
    }
}