using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

//EMGU
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.Util;

//DiresctShow
using DirectShowLib;
using System.IO;
using System.Threading;

namespace CameraCapture
{
    public partial class CameraCapture : Form
    {
        /* Hint use CTL+M and then CTL+O to callapse all fields */
        #region Variables
        Mat frameAnterior = null;
        int contadorFrames = -1;
        int frameDelay = 2;
        bool salvaFrame = false;
        #region Camera Capture Variables
        private Capture _capturePlay = null; //Camera
        private Capture _captureRecord = null; //Camera
        private bool _captureInProgress = false; //Variable to track camera state
        int CameraDevice = 0; //Variable to track camera device selected
        Video_Device[] WebCams; //List containing all the camera available
        #endregion
        VideoWriter vw = null;
        #region Camera Settings
        int Brightness_Store = 0;
        int Contrast_Store = 0;
        int Sharpness_Store = 0;
        #endregion
        #endregion

        public CameraCapture()
        {
            InitializeComponent();

            //-> Find systems cameras with DirectShow.Net dll
            //thanks to carles lloret
            DsDevice[] _SystemCamereas = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            WebCams = new Video_Device[_SystemCamereas.Length];
            for (int i = 0; i < _SystemCamereas.Length; i++)
            {
                WebCams[i] = new Video_Device(i, _SystemCamereas[i].Name, _SystemCamereas[i].ClassID); //fill web cam array
                Camera_Selection.Items.Add(WebCams[i].ToString());
            }
            if (Camera_Selection.Items.Count > 0)
            {
                Camera_Selection.SelectedIndex = 0; //Set the selected device the default
                captureButton.Enabled = true; //Enable the start
                recButton.Enabled = true; //Enable the rec
            }

        }

        /// <summary>
        /// What to do with each frame aquired from the camera
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        private void ProcessFrame(object sender, EventArgs arg)
        {
            //***If you want to access the image data the use the following method call***/
            //Image<Bgr, Byte> frame = new Image<Bgr,byte>(_capture.RetrieveBgrFrame().ToBitmap());

            // Image<Bgr, Byte> frame = _capture.RetrieveBgrFrame();
            Mat frame = new Mat();
            _capturePlay.Retrieve(frame);
            //because we are using an autosize picturebox we need to do a thread safe update
            DisplayImage(frame.Bitmap);
        }

        private void RecordFrame(object sender, EventArgs arg)
        {
            Mat frame = new Mat();
            bool newFrame = _captureRecord.Retrieve(frame);
            if (contadorFrames < 0)
            {
                frameAnterior = frame.Clone();
                contadorFrames = 0;
            }

            try
            {
                if (newFrame && _captureInProgress)
                {
                    DisplayImage(frame.Bitmap);

                    using (Image<Bgr, byte> imageDiff = frame.ToImage<Bgr, byte>().AbsDiff(frameAnterior.ToImage<Bgr, byte>()))
                    {
                        // SI FRAME ES NEGRO => IGNORAR
                        if (frame.ToImage<Bgr, byte>().Split()[0].GetSum().Intensity < frame.Height * frame.Height * 50)
                        {
                            contadorFrames++;
                            return;
                        }

                        // SI FRAME ES SIMILAR AL ANTERIOR => IGNORAR
                        MCvScalar diff = imageDiff.GetSum().MCvScalar;
                        if (diff.V0 > 3000000)
                        {
                            contadorFrames++;
                            salvaFrame = true;
                            frameAnterior = frame.Clone();
                        }
                    };

                    if (salvaFrame)
                    {
                        frameDelay--;
                        if (frameDelay == 0)
                        {
                            vw.Write(frame);
                            salvaFrame = false;
                            frameDelay = 2;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        /// <summary>
        /// Thread safe method to display image in a picturebox that is set to automatic sizing
        /// </summary>
        /// <param name="Image"></param>
        private delegate void DisplayImageDelegate(Bitmap Image);
        private void DisplayImage(Bitmap Image)
        {
            if (captureBox.InvokeRequired)
            {
                try
                {
                    DisplayImageDelegate DI = new DisplayImageDelegate(DisplayImage);
                    this.BeginInvoke(DI, new object[] { Image });
                }
                catch (Exception ex)
                {
                }
            }
            else
            {
                captureBox.Image = Image;
            }
        }

        /// <summary>
        /// Start/Stop the camera aquasition
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void captureButtonClick(object sender, EventArgs e)
        {
            if (_capturePlay != null)
            {
                if (_captureInProgress)
                {
                    //stop the capture
                    captureButton.Text = "Start Capture"; //Change text on button
                    _capturePlay.Pause(); //Pause the capture
                    _captureInProgress = false; //Flag the state of the camera
                    captureButton.Enabled = true;
                    recButton.Enabled = true;
                }
                else
                {
                    //Check to see if the selected device has changed
                    if (Camera_Selection.SelectedIndex != CameraDevice)
                    {
                        SetupCapture(Camera_Selection.SelectedIndex); //Setup capture with the new device
                    }

                    captureButton.Text = "Stop"; //Change text on button
                    _capturePlay.Start(); //Start the capture
                    _captureInProgress = true; //Flag the state of the camera
                    captureButton.Enabled = true;
                    recButton.Enabled = false;
                }

            }
            else
            {
                //set up capture with selected device
                SetupCapture(Camera_Selection.SelectedIndex);
                //Be lazy and Recall this method to start camera
                captureButtonClick(null, null);
            }
        }

        /// <summary>
        /// Sets up the _capture variable with the selected camera index
        /// </summary>
        /// <param name="Camera_Identifier"></param>
        private void SetupCapture(int Camera_Identifier)
        {
            //update the selected device
            CameraDevice = Camera_Identifier;

            //Dispose of Capture if it was created before
            if (_capturePlay != null) _capturePlay.Dispose();
            try
            {
                //Set up capture device
                _capturePlay = new Capture(CameraDevice);
                _capturePlay.ImageGrabbed += ProcessFrame;
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }


        /// <summary>
        /// Sets up the _capture variable with the selected camera index
        /// </summary>
        /// <param name="Camera_Identifier"></param>
        private void SetupRecording(int Camera_Identifier)
        {
            //update the selected device
            CameraDevice = Camera_Identifier;

            //Dispose of Capture if it was created before
            if (_captureRecord != null) _captureRecord.Dispose();
            try
            {
                //Set up capture device
                _captureRecord = new Capture(CameraDevice);
                _captureRecord.ImageGrabbed += RecordFrame;
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }

        private void SetupVideoWriter()
        {
            try
            {
                if (_captureRecord != null)
                {
                    SaveFileDialog dlg = new SaveFileDialog();
                    dlg.Filter = "Avi (*.avi)|*.avi";
                    // If the file name is not an empty string open it for saving.
                    dlg.ShowDialog();
                    if (dlg.FileName != "")
                    {
                        int videoW = (int)_captureRecord.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth);
                        int videoH = (int)_captureRecord.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight);
                        int videoFps = 1;
                        string _path = dlg.FileName;
                        string saveFileName = Path.Combine(Path.GetDirectoryName(_path), Path.GetFileName(_path).Replace(Path.GetExtension(_path), "") + ".avi");
                        vw = new VideoWriter(saveFileName, -1, videoFps, new Size(videoW, videoH), true);
                    }
                }
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }


        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_captureRecord != null)
            {
                _captureRecord.Dispose();
                _capturePlay.Dispose();
            }
            Hide();
        }

        private void recButton_Click(object sender, EventArgs e)
        {
            if (_captureRecord != null)
            {
                if (_captureInProgress)
                {
                    //stop the capture
                    recButton.Text = "Rec Capture"; //Change text on button
                    _captureRecord.Pause(); //Pause the capture
                    _captureInProgress = false; //Flag the state of the camera
                    captureButton.Enabled = true;
                    recButton.Enabled = true;
                    vw.Dispose();

                    MessageBox.Show("NUM.FRAMES GRABADOS: " + contadorFrames);
                    frameAnterior = null;
                    contadorFrames = -1;
                    frameDelay = 2;
                    salvaFrame = false;
                }
                else
                {
                    //Check to see if the selected device has changed
                    if (Camera_Selection.SelectedIndex != CameraDevice)
                        SetupRecording(Camera_Selection.SelectedIndex); //Setup capture with the new device

                    SetupVideoWriter();

                    recButton.Text = "Stop"; //Change text on button
                    _captureRecord.Start(); //Start the capture
                    _captureInProgress = true; //Flag the state of the camera
                    captureButton.Enabled = false;
                    recButton.Enabled = true;
                }

            }
            else
            {
                //set up capture with selected device
                SetupRecording(Camera_Selection.SelectedIndex);
                //Be lazy and Recall this method to start camera
                recButton_Click(null, null);
            }
        }

    }
}

