using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ESBasic;
using Oraycn.MCapture;
using Oraycn.MFile;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace Oraycn.WPF.RecordDemo
{
    /// <summary>
    /// 环境 4.0，显示框架WPF  语言C#
    /// </summary>
    public partial class MainWindow : Window
    {
        private ISoundcardCapturer soundcardCapturer;
        private IMicrophoneCapturer microphoneCapturer;
        private IDesktopCapturer desktopCapturer;
        private ICameraCapturer cameraCapturer;
        private IAudioMixter audioMixter;
        private VideoFileMaker videoFileMaker;
        private SilenceVideoFileMaker silenceVideoFileMaker;
        private AudioFileMaker audioFileMaker;
        private int frameRate = 10; // 采集视频的帧频
        private bool sizeRevised = false;// 是否需要将图像帧的长宽裁剪为4的整数倍
        private volatile bool isRecording = false;
        private volatile bool isParsing = false;
        private Timer timer;
        private int seconds = 0;
        private bool justRecordVideo = false;
        private bool justRecordAudio = false;

        Random r = new Random();
        public MainWindow()
        {
            InitializeComponent();

            Oraycn.MCapture.GlobalUtil.SetAuthorizedUser("FreeUser", "");
            Oraycn.MFile.GlobalUtil.SetAuthorizedUser("FreeUser", "");

            this.timer = new Timer();
            this.timer.Interval = 1000;
            this.timer.Tick += timer_Tick;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            if (this.isRecording && !this.isParsing)
            {
                var ts = new TimeSpan(0, 0, ++seconds);
                this.label.Content = ts.Hours.ToString("00") + ":" + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00");
            }
        }

        #region 开始
        private void button_Start_Click(object sender, RoutedEventArgs e)
        {
            //TODO 开始录制桌面，依据 声音复选框 来选择使用 声卡 麦克风 还是混合录制
            //TODO label 中显示实际录制的时间，需要考虑暂停和恢复这种情况。 格式为 hh:mm:ss
            if(string.IsNullOrEmpty(this.filePath.Text)|| string.IsNullOrEmpty(this.fileNmae.Text))
            {
                MessageBox.Show("文件路径和文件名！");
                return;
            }
            else if(File.Exists(System.IO.Path.Combine(this.filePath.Text, this.fileNmae.Text)))
            {
                MessageBox.Show("文件已经存在");
                return;
            }
            try
            {
                int audioSampleRate = 16000;
                int channelCount = 1;
                seconds = 0;

                System.Drawing.Size videoSize =  Screen.PrimaryScreen.Bounds.Size; 
                this.justRecordAudio = this.radioButton_justAudio.IsChecked.Value;

                if (this.justRecordAudio && this.checkBox_micro.IsChecked == false && this.checkBox_soundCard.IsChecked == false)
                {
                    MessageBox.Show("一定要选择一个声音的采集源！");
                    return;
                }

                #region 设置采集器
                if (this.radioButton_desktop.IsChecked == true)
                {
                    //桌面采集器
                    
                    //如果需要录制鼠标的操作，第二个参数请设置为true
                    this.desktopCapturer = CapturerFactory.CreateDesktopCapturer(frameRate, false);
                    this.desktopCapturer.ImageCaptured += ImageCaptured;
                    videoSize = this.desktopCapturer.VideoSize;
                }
                else if (this.radioButton_camera.IsChecked == true)
                {
                    //摄像头采集器
                    videoSize = new System.Drawing.Size(int.Parse(this.textBox_width.Text), int.Parse(this.textBox_height.Text));
                    this.cameraCapturer = CapturerFactory.CreateCameraCapturer(0, videoSize, frameRate);
                    this.cameraCapturer.ImageCaptured += new CbGeneric<Bitmap>(ImageCaptured);
                }

                if (this.checkBox_micro.IsChecked == true)
                {
                    //麦克风采集器
                    this.microphoneCapturer = CapturerFactory.CreateMicrophoneCapturer(0);
                    this.microphoneCapturer.CaptureError += capturer_CaptureError;
                }

                if (this.checkBox_soundCard.IsChecked == true)
                {
                    //声卡采集器 【目前声卡采集仅支持vista以及以上系统】
                    this.soundcardCapturer = CapturerFactory.CreateSoundcardCapturer();
                    this.soundcardCapturer.CaptureError += capturer_CaptureError;
                    audioSampleRate = this.soundcardCapturer.SampleRate;
                    channelCount = this.soundcardCapturer.ChannelCount;
                }

                if (this.checkBox_micro.IsChecked == true && this.checkBox_soundCard.IsChecked == true)
                {
                    //混音器
                    this.audioMixter = CapturerFactory.CreateAudioMixter(this.microphoneCapturer, this.soundcardCapturer, SoundcardMode4Mix.DoubleChannel, true);
                    this.audioMixter.AudioMixed += audioMixter_AudioMixed; //如果是混音,则不再需要预订microphoneCapturer和soundcardCapturer的AudioCaptured事件
                    audioSampleRate = this.audioMixter.SampleRate;
                    channelCount = this.audioMixter.ChannelCount;
                }
                else if (this.checkBox_micro.IsChecked == true)
                {
                    this.microphoneCapturer.AudioCaptured += audioMixter_AudioMixed;
                }
                else if (this.checkBox_soundCard.IsChecked == true)
                {
                    this.soundcardCapturer.AudioCaptured += audioMixter_AudioMixed;
                } 
                #endregion
                
                #region 开始采集
                if (this.checkBox_micro.IsChecked == true)
                {
                    this.microphoneCapturer.Start();
                }
                if (this.checkBox_soundCard.IsChecked == true)
                {
                    this.soundcardCapturer.Start();
                }

                if (this.radioButton_camera.IsChecked == true)
                {
                    this.cameraCapturer.Start();
                }
                else if (this.radioButton_desktop.IsChecked == true)
                {
                    this.desktopCapturer.Start();
                } 
                #endregion
                
                #region 录制组件
                if (this.justRecordAudio) //仅仅录制声音
                {
                    this.audioFileMaker = new AudioFileMaker();
                    this.audioFileMaker.Initialize("test.mp3", audioSampleRate, channelCount);
                }
                else
                {
                    //宽和高修正为4的倍数
                    this.sizeRevised = (videoSize.Width % 4 != 0) || (videoSize.Height % 4 != 0);
                    if (this.sizeRevised)
                    {
                        videoSize = new System.Drawing.Size(videoSize.Width / 4 * 4, videoSize.Height / 4 * 4);
                    }

                    if (this.checkBox_micro.IsChecked == false && this.checkBox_soundCard.IsChecked == false) //仅仅录制图像
                    {
                        this.justRecordVideo = true;
                        this.silenceVideoFileMaker = new SilenceVideoFileMaker();
                        this.silenceVideoFileMaker.Initialize(System.IO.Path.Combine(this.filePath.Text, this.fileNmae.Text)+".mp4", VideoCodecType.H264, videoSize.Width, videoSize.Height, frameRate, VideoQuality.Middle);
                    }
                    else //录制声音+图像
                    {
                        this.justRecordVideo = false;
                        this.videoFileMaker = new VideoFileMaker();
                        this.videoFileMaker.Initialize(System.IO.Path.Combine(this.filePath.Text, this.fileNmae.Text) + ".mp4", VideoCodecType.H264, videoSize.Width, videoSize.Height, frameRate, VideoQuality.High, AudioCodecType.AAC, audioSampleRate, channelCount, true);

                    }
                } 
                #endregion             
               
                this.isRecording = true;
                this.isParsing = false;
                this.timer.Start();

                this.checkBox_micro.IsEnabled = false;
                this.checkBox_soundCard.IsEnabled = false;
                this.radioButton_desktop.IsEnabled = false;
                this.radioButton_camera.IsEnabled = false;
                this.radioButton_justAudio.IsEnabled = false;

                this.button_Start.IsEnabled = false;
            }
            catch (Exception ee)
            {
                MessageBox.Show(ee.Message);
            }
        } 
        #endregion

        #region 暂停
        private void button_Parse_Click(object sender, RoutedEventArgs e)
        {
            //TODO 暂停当前录制或恢复录制
            //TODO label 中显示实际录制的时间，需要考虑暂停和恢复这种情况。 格式为 hh:mm:ss
            if (this.isParsing)
            {
                this.isParsing = false;
            }
            else
            {
                this.isParsing = true;
            }
            this.button_Parse.Content = (!this.isParsing?  "暂停" : "恢复");
           

        } 
        #endregion

        #region 结束
        private void button_Stop_Click(object sender, RoutedEventArgs e)
        {
            ////TODO 结束录制，保存文件 
            this.checkBox_micro.IsEnabled = true;
            this.checkBox_soundCard.IsEnabled = true;
            this.radioButton_camera.IsEnabled = true;
            this.radioButton_desktop.IsEnabled = true;
            this.radioButton_justAudio.IsEnabled = true;

            this.button_Start.IsEnabled = true;

            this.button_Parse.Content = "暂停";

            if (this.checkBox_micro.IsChecked == true) // 麦克风
            {
                this.microphoneCapturer.Stop();
            }
            if (this.checkBox_soundCard.IsChecked == true) //声卡
            {
                this.soundcardCapturer.Stop();
            }
            if (this.radioButton_camera.IsChecked == true)
            {
                this.cameraCapturer.Stop();
            }
            if (this.radioButton_desktop.IsChecked == true)
            {
                this.desktopCapturer.Stop();
            }
            if (this.justRecordAudio)
            {
                this.audioFileMaker.Close(true);
            }
            else
            {
                if (!this.justRecordVideo)
                {
                    this.videoFileMaker.Close(true);
                }
                else
                {
                    this.silenceVideoFileMaker.Close(true);
                }
            }
            
            this.isRecording = false;
            MessageBox.Show("录制完成！");
        } 
        #endregion

        void audioMixter_AudioMixed(byte[] audioData)
        {
            if (this.isRecording && !this.isParsing)
            {
                if (this.justRecordAudio)
                {
                    this.audioFileMaker.AddAudioFrame(audioData);
                }
                else
                {
                    if (!this.justRecordVideo)
                    {
                        this.videoFileMaker.AddAudioFrame(audioData);
                    }
                }
                
            }
        }

        void capturer_CaptureError(Exception ex)
        {

        }

        #region ImageCaptured
        void ImageCaptured(Bitmap bm)
        {
            if (this.isRecording && !this.isParsing)
            {
                //这里可能要裁剪
                Bitmap imgRecorded = bm;
                if (this.sizeRevised) // 对图像进行裁剪，  MFile要求录制的视频帧的长和宽必须是4的整数倍。
                {
                    imgRecorded = ESBasic.Helpers.ImageHelper.RoundSizeByNumber(bm, 4);
                    bm.Dispose();
                }

                if (!this.justRecordVideo)
                {
                    this.videoFileMaker.AddVideoFrame(imgRecorded);
                }
                else
                {
                    this.silenceVideoFileMaker.AddVideoFrame(imgRecorded);
                }
            }
        } 
        #endregion

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (this.isRecording)
            {
                MessageBox.Show("正在录制视频，请先停止！");
                e.Cancel = true;
                return;
            }

            e.Cancel = false;

        }

        #region radioButton_camera_Checked
        private void radioButton_camera_Checked(object sender, RoutedEventArgs e)
        {
            this.label1.Visibility = Visibility.Visible;
            this.label2.Visibility = Visibility.Visible;
            this.textBox_width.Visibility = Visibility.Visible;
            this.textBox_height.Visibility = Visibility.Visible;
        } 
        #endregion

        #region radioButton_desktop_Checked
        private void radioButton_desktop_Checked(object sender, RoutedEventArgs e)
        {
            this.label1.Visibility = Visibility.Hidden;
            this.label2.Visibility = Visibility.Hidden;
            this.textBox_width.Visibility = Visibility.Hidden;
            this.textBox_height.Visibility = Visibility.Hidden;
        } 
        #endregion

        #region radioButton_justAudio_Checked
        private void radioButton_justAudio_Checked(object sender, RoutedEventArgs e)
        {
            this.label1.Visibility = Visibility.Hidden;
            this.label2.Visibility = Visibility.Hidden;
            this.textBox_width.Visibility = Visibility.Hidden;
            this.textBox_height.Visibility = Visibility.Hidden;
        }


        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FilePath_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "请选择文件路径";
            dialog.ShowDialog();
            if (!string.IsNullOrEmpty(dialog.SelectedPath))
            {
                this.filePath.Text= dialog.SelectedPath;
            }
        }
    }
}
