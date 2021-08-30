using System;
using System.Diagnostics;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Media.Devices;
using Windows.Media.Capture;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Windows.UI;
using Windows.Media.Audio;
using CustomEffect;
using Windows.Media.Effects;
using Windows.Media.Render;
using Windows.Storage;
using Windows.Storage.Pickers;


namespace EvenFieldAudio
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private DeviceInformationCollection outputDevices;

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await PopulateOutputDeviceList();
            //await AudioGraphManager.UpdateGraph();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (AudioGraphManager.audioGraph != null)
            {
                AudioGraphManager.audioGraph.Dispose();
            }
        }


        private async Task PopulateOutputDeviceList()
        {
            outputDevicesListBox.Items.Clear();
            outputDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
            outputDevicesListBox.Items.Add("-- Pick output device --");
            foreach (var device in outputDevices)
            {
                outputDevicesListBox.Items.Add(device.Name);
            }
        }
        private async void OutputDevicesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeviceInformation outputDevice = outputDevices[outputDevicesListBox.SelectedIndex - 1];
            AudioGraphManager.outputDeviceInfo = outputDevice;
            await AudioGraphManager.UpdateGraph();
        }

        private async void File_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            filePicker.FileTypeFilter.Add(".mp3");
            filePicker.FileTypeFilter.Add(".wav");
            filePicker.FileTypeFilter.Add(".wma");
            filePicker.FileTypeFilter.Add(".m4a");
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            StorageFile inputFile = await filePicker.PickSingleFileAsync();
            SelectFile.Background = new SolidColorBrush(Colors.Green);

            AudioGraphManager.inputFile = inputFile;
            await AudioGraphManager.UpdateGraph();
        }

        private void PlayDemo_Click(object sender, RoutedEventArgs e)
        {            
            // Toggle playback
            if (PlayDemo.Content.Equals("Play Demo"))
            {
                AudioGraphManager.audioGraph.Start();
                PlayDemo.Content = "Stop Demo";
            }
            else
            {
                AudioGraphManager.audioGraph.Stop();
                PlayDemo.Content = "Play Demo";
            }
        }

        // TODO: Event handler for file completion event
        private async void FileInput_FileCompleted(AudioFileInputNode sender, object args)
        {
            // File playback is done. Stop the graph
            AudioGraphManager.audioGraph.Stop();

            // Reset the file input node so starting the graph will resume playback from beginning of the file
            sender.Reset();

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                PlayDemo.Content = "Play Demo";
            });
        }

        private void LeftEarCondition_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // TODO: set on graph manager and update
        }

        private void RightEarCondition_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // TODO: set on graph manager and update
        }

        private void EffectType_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // TODO: set on graph manager and update
        }

        private void EffectType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }

    public static class AudioGraphManager
    {
        public static AudioDeviceInputNode inputDeviceNode;
        public static StorageFile inputFile;
        public static IAudioInputNode inputNode;
        public static DeviceInformation outputDeviceInfo;
        public static AudioDeviceOutputNode deviceOutputNode;
        public static AudioGraph audioGraph;

        public async static Task<AudioGraph> UpdateGraph()
        {
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);

            // Set output device information
            if (outputDeviceInfo != null)
            {
                settings.PrimaryRenderDevice = outputDeviceInfo;
            }

            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);
            if (result.Status != AudioGraphCreationStatus.Success)
            {
                // Cannot create graph
                
                throw new Exception("Cannot create audio graph");
            }
            audioGraph = result.Graph;

            // Create a device output node
            CreateAudioDeviceOutputNodeResult deviceOutputResult = await audioGraph.CreateDeviceOutputNodeAsync();
            if (deviceOutputResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                // Cannot create device output
                throw new Exception("Cannot create output node");
            }
            deviceOutputNode = deviceOutputResult.DeviceOutputNode;

            // Create a device input node
            if (inputFile == null)
            {
                // if no file selected use device default audio
                // TODO: make default the sound card insted of the microphone
                CreateAudioDeviceInputNodeResult inputDeviceResult = await audioGraph.CreateDeviceInputNodeAsync(MediaCategory.Media);
                if (inputDeviceResult.Status != AudioDeviceNodeCreationStatus.Success)
                {
                    throw new Exception("Could not create default device input node: {0}");
                }
                inputNode = inputDeviceResult.DeviceInputNode;
            }
            else
            {
                CreateAudioFileInputNodeResult fileInputNodeResult = await audioGraph.CreateFileInputNodeAsync(inputFile);
                if (fileInputNodeResult.Status != AudioFileNodeCreationStatus.Success)
                {
                    // Cannot read file
                    throw new Exception("could not read file input node");
                }

                inputNode = fileInputNodeResult.FileInputNode;
            }
            inputNode.AddOutgoingConnection(deviceOutputNode);

            // Create the custom effect and apply to the FileInput node
            //AddCustomEffect();

            return audioGraph;
        }
        private static void AddCustomEffect()
        {
            // Create a property set and add a property/value pair
            PropertySet echoProperties = new PropertySet();
            echoProperties.Add("Mix", 0.5f);

            // Instantiate the custom effect defined in the 'CustomEffect' project
            AudioEffectDefinition echoEffectDefinition = new AudioEffectDefinition(typeof(AudioEchoEffect).FullName, echoProperties);
            inputNode.EffectDefinitions.Add(echoEffectDefinition);
        }
    }
}

