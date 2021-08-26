using System;
using System.Diagnostics;
using System.Collections.Generic;
using Windows.Media.Effects;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.MediaProperties;
using System.Runtime.InteropServices;
using NWaves;


namespace CustomEffect
{
    // Using the COM interface IMemoryBufferByteAccess allows us to access the underlying byte array in an AudioFrame
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public sealed class AudioEchoEffect : IBasicAudioEffect
    {
        private AudioEncodingProperties currentEncodingProperties;
        private readonly List<AudioEncodingProperties> supportedEncodingProperties;

        private SampleProviders.SignalGenerator signalGen;
        private int frameCount;
        private IPropertySet propertySet;

        public bool UseInputFrameForOutput { get { return false; } }

        // Set up constant members in the constructor
        public AudioEchoEffect()
        {
            // Support 44.1kHz and 48kHz mono float
            supportedEncodingProperties = new List<AudioEncodingProperties>();
            AudioEncodingProperties encodingProps1 = AudioEncodingProperties.CreatePcm(44100, 1, 32);
            encodingProps1.Subtype = MediaEncodingSubtypes.Float;
            AudioEncodingProperties encodingProps2 = AudioEncodingProperties.CreatePcm(48000, 1, 32);
            encodingProps2.Subtype = MediaEncodingSubtypes.Float;
            AudioEncodingProperties encodingProps3 = AudioEncodingProperties.CreatePcm(44100, 2, 32);
            encodingProps3.Subtype = MediaEncodingSubtypes.Float;
            AudioEncodingProperties encodingProps4 = AudioEncodingProperties.CreatePcm(48000, 2, 32);
            encodingProps4.Subtype = MediaEncodingSubtypes.Float;

            supportedEncodingProperties.Add(encodingProps1);
            supportedEncodingProperties.Add(encodingProps2);
            supportedEncodingProperties.Add(encodingProps3);
            supportedEncodingProperties.Add(encodingProps4);
        }
        
        public IReadOnlyList<AudioEncodingProperties> SupportedEncodingProperties
        {
            get
            {
                return supportedEncodingProperties;
            }
        }

        public void SetEncodingProperties(AudioEncodingProperties encodingProperties)
        {
            currentEncodingProperties = encodingProperties;
            frameCount = 0;

            signalGen = new SampleProviders.SignalGenerator(this.currentEncodingProperties);
            signalGen.Type = SampleProviders.SignalGeneratorType.Sin;

        }

        unsafe public void ProcessFrame(ProcessAudioFrameContext context)
        {
            AudioFrame inputFrame = context.InputFrame;
            AudioFrame outputFrame = context.OutputFrame;

            using (AudioBuffer inputBuffer = inputFrame.LockBuffer(AudioBufferAccessMode.Read),
                                outputBuffer = outputFrame.LockBuffer(AudioBufferAccessMode.Write))
            using (IMemoryBufferReference inputReference = inputBuffer.CreateReference(),
                                            outputReference = outputBuffer.CreateReference())
            {
                if (this.currentEncodingProperties.ChannelCount <= 1)
                {
                    // can't create stereo contrast without stereo channels...
                    return;
                }

                // Get the audio buffer reference
                byte* inputDataInBytes;
                byte* outputDataInBytes;
                uint inputCapacity;
                uint outputCapacity;

                ((IMemoryBufferByteAccess)inputReference).GetBuffer(out inputDataInBytes, out inputCapacity);
                ((IMemoryBufferByteAccess)outputReference).GetBuffer(out outputDataInBytes, out outputCapacity);

                float* inputDataInFloat = (float*)inputDataInBytes;
                float* outputDataInFloat = (float*)outputDataInBytes;

                // Compute the size of the values array containing the audio samples based on the bitrate
                // which tells us how big the float values are in the array
                int valueSize;
                if (currentEncodingProperties.BitsPerSample == 32)
                {
                    valueSize = sizeof(float);
                } else if (currentEncodingProperties.BitsPerSample == 16)
                {
                    valueSize = sizeof(float)/2;
                } else
                {
                    throw new Exception("Unsupported bits per sample");
                }
                int dataInFloatLength = (int)inputBuffer.Length / valueSize;

                // Stereo delta is the difference in audio values between the left and right channels.
                // When stereo delta is negative that means left, and positive means right.
                // This matters because the difference in the audio channels can be added to the
                // mono mix of the two channels so that when the channels are different it can be heard
                // and so left can therefore be differentiated from right
                float left;
                float right;
                float stereoDelta;

                // we only want to edit left and right channels so we need to only get the first 2 samples of each sample packet
                int valuesInSample = (int)currentEncodingProperties.ChannelCount;

                // SignalGenerator manages converting the value counts to sample counts and returns a buffer
                // of the same size as the number of samples
                float[] sinBuffer = signalGen.Read(0, dataInFloatLength);

                //int samplesCount = dataInFloatLength / sampleSize;
                for (int i = 0; i < dataInFloatLength - 1; i += valuesInSample)
                {
                    left = inputDataInFloat[i];
                    right = inputDataInFloat[i + 1];
                    // Use stereo delta to compute some kind of effect, if right or left are bigger
                    // we can apply different effects
                    stereoDelta = Math.Abs(right) - Math.Abs(left);

                    // Projection

                    // Channel contrast
                    outputDataInFloat[i] = sinBuffer[i] * left + right;

                    // Disimilarity contrast
                    //outputDataInFloat[i] = 2*stereoDelta * sinBuffer[i] + (left + right);
                    // Pretend right is deaf and zero it out
                    outputDataInFloat[i + 1] = 0f;
                }
                frameCount += 1;
            }
        }

        public void Close(MediaEffectClosedReason reason)
        {
            // Clean-up any effect resources
            // This effect doesn't care about close, so there's nothing to do
        }

        public void DiscardQueuedFrames()
        {
            // Reset contents of the samples buffer
            frameCount = 0;
        }

        public void SetProperties(IPropertySet configuration)
        {
            this.propertySet = configuration;
        }
    }
}