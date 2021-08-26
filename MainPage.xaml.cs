using System;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Media.Devices;
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
        private MainPage rootPage;
        private DeviceInformationCollection outputDevices;
        private AudioDeviceOutputNode deviceOutputNode;
        private AudioGraph graph;
        private AudioFileInputNode fileInputNode;

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await CreateAudioGraph();
            await PopulateOutputDeviceList();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (graph != null)
            {
                graph.Dispose();
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
        private void outputDevicesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: Update audio graph and resume playback
        }

        private async void File_Click(object sender, RoutedEventArgs e)
        {
            await SelectInputFile();
        }

        private async Task SelectInputFile()
        {
            // If another file is already loaded into the FileInput node
            if (fileInputNode != null)
            {
                // Release the file and dispose the contents of the node
                fileInputNode.Dispose();
                // Stop playback since a new file is being loaded. Also reset the button UI
                if (PlayDemo.Content.Equals("Stop Demo"))
                {
                    TogglePlay();
                }
            }

            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            filePicker.FileTypeFilter.Add(".mp3");
            filePicker.FileTypeFilter.Add(".wav");
            filePicker.FileTypeFilter.Add(".wma");
            filePicker.FileTypeFilter.Add(".m4a");
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            StorageFile file = await filePicker.PickSingleFileAsync();

            // File can be null if cancel is hit in the file picker
            if (file == null)
            {
                return;
            }

            CreateAudioFileInputNodeResult fileInputNodeResult = await graph.CreateFileInputNodeAsync(file);
            if (fileInputNodeResult.Status != AudioFileNodeCreationStatus.Success)
            {
                // Cannot read file
                //NotifyUser(String.Format("Cannot read input file because {0}", fileInputNodeResult.Status.ToString()), NotifyType.ErrorMessage);
                return;
            }

            fileInputNode = fileInputNodeResult.FileInputNode;
            fileInputNode.AddOutgoingConnection(deviceOutputNode);
            SelectFile.Background = new SolidColorBrush(Colors.Green);

            // Event Handler for file completion
            fileInputNode.FileCompleted += FileInput_FileCompleted;

            // Enable the button to start the graph
            PlayDemo.IsEnabled = true;

            // Create the custom effect and apply to the FileInput node
            AddCustomEffect();
        }

        private void Graph_Click(object sender, RoutedEventArgs e)
        {
            TogglePlay();
        }

        private void TogglePlay()
        {
            // Toggle playback
            if (PlayDemo.Content.Equals("Play Demo"))
            {
                graph.Start();
                PlayDemo.Content = "Stop Demo";
            }
            else
            {
                graph.Stop();
                PlayDemo.Content = "Play Demo";
            }
        }

        private async Task CreateAudioGraph()
        {
            // Create an AudioGraph with default settings
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media);
            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);

            if (result.Status != AudioGraphCreationStatus.Success)
            {
                // Cannot create graph
                //rootPage.NotifyUser(String.Format("AudioGraph Creation Error because {0}", result.Status.ToString()), NotifyType.ErrorMessage);
                return;
            }

            graph = result.Graph;

            // Create a device output node
            CreateAudioDeviceOutputNodeResult deviceOutputResult = await graph.CreateDeviceOutputNodeAsync();

            if (deviceOutputResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                // Cannot create device output
                //rootPage.NotifyUser(String.Format("Audio Device Output unavailable because {0}", deviceOutputResult.Status.ToString()), NotifyType.ErrorMessage);
                return;
            }

            deviceOutputNode = deviceOutputResult.DeviceOutputNode;
            //rootPage.NotifyUser("Device Output Node successfully created", NotifyType.StatusMessage);
        }

        private void AddCustomEffect()
        {
            // Create a property set and add a property/value pair
            PropertySet echoProperties = new PropertySet();
            echoProperties.Add("Mix", 0.5f);

            // Instantiate the custom effect defined in the 'CustomEffect' project
            AudioEffectDefinition echoEffectDefinition = new AudioEffectDefinition(typeof(AudioEchoEffect).FullName, echoProperties);
            fileInputNode.EffectDefinitions.Add(echoEffectDefinition);
        }

        // Event handler for file completion event
        private async void FileInput_FileCompleted(AudioFileInputNode sender, object args)
        {
            // File playback is done. Stop the graph
            graph.Stop();

            // Reset the file input node so starting the graph will resume playback from beginning of the file
            sender.Reset();

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                //rootPage.NotifyUser("End of file reached", NotifyType.StatusMessage);
                PlayDemo.Content = "Play Demo";
            });
        }

        private void LeftEarCondition_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // TODO:
        }

        private void RightEarCondition_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // TODO;
        }

        private void PlayDemo_Click(object sender, RoutedEventArgs e)
        {
            //TogglePlay();
        }

        private void EffectType_SelectionChanged(object sender, RoutedEventArgs e)
        {
            // TODO;
        }
    }
}

