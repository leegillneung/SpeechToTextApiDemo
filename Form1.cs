
using NAudio.Wave;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Google.Apis.Auth.OAuth2;
using Google.Cloud.Speech.V1;

using Grpc.Auth;
using Google.Protobuf.Collections;
using System.Threading;
using NAudio.Mixer;

namespace SpeechToTextApiDemo
{
    public partial class Form1 : Form
    {
        private List<string> recordingDevices = new List<string>();
        private AudioRecorder audioRecorder = new AudioRecorder();

        private Boolean monitoring = false;

        private BufferedWaveProvider waveBuffer;

        private WaveInEvent waveIn = new NAudio.Wave.WaveInEvent();


        public Form1()
        {
            InitializeComponent();

            if (NAudio.Wave.WaveIn.DeviceCount < 1)
            {
                MessageBox.Show("No microphone! ... exiting");
                return;
            }

            audioRecorder.SampleAggregator.MaximumCalculated += OnRecorderMaximumCalculated;

            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                recordingDevices.Add(WaveIn.GetCapabilities(n).ProductName);
            }

            waveIn.DeviceNumber = 0;
            waveIn.WaveFormat = new NAudio.Wave.WaveFormat(16000, 1);
            waveIn.DataAvailable += WaveIn_DataAvailable;
            waveBuffer = new BufferedWaveProvider(waveIn.WaveFormat);
            waveBuffer.DiscardOnBufferOverflow = true;

            timer1.Enabled = false;
            timer1.Interval = 1000;
            timer1.Tick += Timer1_Tick;

        }

        void OnRecorderMaximumCalculated(object sender, MaxSampleEventArgs e)
        {
            float peak = Math.Max(e.MaxSample, Math.Abs(e.MinSample));

            peak *= 100;
            progressBar1.Value = (int)peak;

            if (peak > 5)
            {
                if (timer1.Enabled == false)
                {
                    timer1.Enabled = true;
                    waveIn.StartRecording();
                }

            }

        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            waveBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);

        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            waveIn.StopRecording();

            Task me = StreamBufferToGooglesAsync();

        }

        private async Task<object> StreamBufferToGooglesAsync()
        {
            var speech = SpeechClient.Create();
            var streamingCall = speech.StreamingRecognize();

            await streamingCall.WriteAsync(new StreamingRecognizeRequest()
            {
                StreamingConfig = new StreamingRecognitionConfig()
                {
                    Config = new RecognitionConfig()
                    {
                        Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                        SampleRateHertz = 16000,
                        LanguageCode = "en",
                    },

                    SingleUtterance = true,
                }
            });

            byte[] buffer = new byte[waveBuffer.BufferLength];
            int offset = 0;
            int count = waveBuffer.BufferLength;

            waveBuffer.Read(buffer, offset, count);

            try
            {
                streamingCall.WriteAsync(new StreamingRecognizeRequest()
                {
                    AudioContent = Google.Protobuf.ByteString.CopyFrom(buffer, 0, count)
                }).Wait();
            }
            catch (Exception wtf)
            {
                string wtfMessage = wtf.Message;
            }

            Task printResponses = Task.Run(async () =>
            {
                string saidWhat = "";
                string lastSaidWhat = "";
                while (await streamingCall.GetResponseStream().MoveNextAsync(default(CancellationToken)))
                {
                    foreach (var result in streamingCall.GetResponseStream().Current.Results)
                    {
                        foreach (var alternative in result.Alternatives)
                        {
                            saidWhat = alternative.Transcript;
                            if (lastSaidWhat != saidWhat)
                            {
                                Console.WriteLine(saidWhat);
                                lastSaidWhat = saidWhat;
                                textBox1.Invoke((MethodInvoker)delegate { textBox1.AppendText(textBox1.Text + saidWhat + " \r\n"); });
                            }

                        } 

                    }
                }
            });

            waveBuffer.ClearBuffer();
            await streamingCall.WriteCompleteAsync();

            return 0;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (recordingDevices.Count > 0)
            {
                if (monitoring == false)
                {
                    monitoring = true;
                    audioRecorder.BeginMonitoring(0);
                }
                else
                {
                    monitoring = false;
                    audioRecorder.Stop();
                }
            }
        }
    }
}
