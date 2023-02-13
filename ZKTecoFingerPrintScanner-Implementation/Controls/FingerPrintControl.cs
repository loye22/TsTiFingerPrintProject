using libzkfpcsharp;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows.Forms;
using ZKTecoFingerPrintScanner_Implementation;
using ZKTecoFingerPrintScanner_Implementation.Helpers;
using Word = Microsoft.Office.Interop.Word;
using System.Security.Cryptography;
using System.Reflection;




namespace Dofe_Re_Entry.UserControls.DeviceController
{
    public partial class FingerPrintControl : UserControl
    {

        const string VerifyButtonDefault = "Verify";
        const string VerifyButtonToggle = "Stop Verification";
        const string Disconnected = "Disconnected";

        Thread captureThread = null;


        public Master parentForm = null;

        #region -------- FIELDS --------

        const int REGISTER_FINGER_COUNT = 3;

        zkfp fpInstance = new zkfp();
        IntPtr FormHandle = IntPtr.Zero; // To hold the handle of the form
        bool bIsTimeToDie = false;
        bool IsRegister = false;
        bool bIdentify = true;
        byte[] FPBuffer;   // Image Buffer
        int RegisterCount = -1;

        byte[][] RegTmps = new byte[REGISTER_FINGER_COUNT][];

        byte[] RegTmp = new byte[2048];
        byte[] CapTmp = new byte[2048];
        int cbCapTmp = 2048;
        int regTempLen = 0;
        int iFid = 1;
        Bitmap obj;
        int counHelper = -1; 

        const int MESSAGE_CAPTURED_OK = 0x0400 + 6;


        

        private int mfpWidth = 0;
        private int mfpHeight = 0;


        /// <summary>
        bool isInit = false;
        int registerCountHeper = -1;
        bool freeRes = false; 
        

        /// </summary>




        #endregion
        MySqlConnection cnn;
        string CompanyID = "";
        Dictionary<string, string> selected_company;  //  = new Dictionary<string, string>();
        String pathP;
        bool capther =false;

        // [ CONSTRUCTOR ]
        public   FingerPrintControl()
        {
            
           
            InitializeComponent();

            ReInitializeInstance();

           //await initComboBoxItems();

            //   comboBox1.Items.Add("ljxc nzxjkcnjkcn");


        }


        // [ INITALIZE DEVICE ]
        private void bnInit_Click(object sender, EventArgs e)
        {
            parentForm.statusBar.Visible = false;
            cmbIdx.Items.Clear();

            int initializeCallBackCode = fpInstance.Initialize();
            if (zkfp.ZKFP_ERR_OK == initializeCallBackCode)
            {
                int nCount = fpInstance.GetDeviceCount();
                if (nCount > 0)
                {
                    for (int i = 1; i <= nCount; i++) cmbIdx.Items.Add(i.ToString());

                    cmbIdx.SelectedIndex = 0;
                    btnInit.Enabled = false;

                    DisplayMessage(MessageManager.msg_FP_InitComplete, true);
                }
                else
                {
                    int finalizeCount = fpInstance.Finalize();
                    DisplayMessage(MessageManager.msg_FP_NotConnected, false);
                }




                // CONNECT DEVICE

                #region -------- CONNECT DEVICE --------

                int openDeviceCallBackCode = fpInstance.OpenDevice(cmbIdx.SelectedIndex);
                if (zkfp.ZKFP_ERR_OK != openDeviceCallBackCode)
                {
                    DisplayMessage($"Uable to connect with the device! (Return Code: {openDeviceCallBackCode} )", false);
                    return;
                }

                Utilities.EnableControls(false, btnInit);
                Utilities.EnableControls(true, btnClose, btnEnroll, btnVerify, btnIdentify, btnFree);

              //  RegisterCount = 0;
                regTempLen = 0;
                iFid = 1;

                //for (int i = 0; i < 3; i++)
                for (int i = 0; i < REGISTER_FINGER_COUNT; i++)
                {
                    RegTmps[i] = new byte[2048];
                }

                byte[] paramValue = new byte[4];
                int size = 4;

                //fpInstance.GetParameters

                fpInstance.GetParameters(1, paramValue, ref size);
                zkfp2.ByteArray2Int(paramValue, ref mfpWidth);

                size = 4;
                fpInstance.GetParameters(2, paramValue, ref size);
                zkfp2.ByteArray2Int(paramValue, ref mfpHeight);

                FPBuffer = new byte[mfpWidth * mfpHeight];

                //FPBuffer = new byte[fpInstance.imageWidth * fpInstance.imageHeight];

                captureThread = new Thread(new ThreadStart(DoCapture));
                captureThread.IsBackground = true;
                captureThread.Start();


                bIsTimeToDie = false;

                string devSN = fpInstance.devSn;
                lblDeviceStatus.Text = "Connected \nDevice S.No: " + devSN;

                DisplayMessage("You are now connected to the device.", true);

                this.isInit = true;
                this.registerCountHeper = RegisterCount;



                #endregion

            }
            else
                DisplayMessage("Unable to initailize the device. Make sure the device is connected " + FingerPrintDeviceUtilities.DisplayDeviceErrorByCode(initializeCallBackCode) + " !! ", false);

        }



        // [ CAPTURE FINGERPRINT ]
        private void DoCapture()
        {
            try
            {
                while (!bIsTimeToDie)
                {
                    cbCapTmp = 2048;
                    int ret = fpInstance.AcquireFingerprint(FPBuffer, CapTmp, ref cbCapTmp);

                    if (ret == zkfp.ZKFP_ERR_OK)
                    {
                        //if (RegisterCount == 0)
                        //    btnEnroll.Invoke((Action)delegate
                        //    {
                        //        btnEnroll.Enabled = true;
                        //    });
                        SendMessage(FormHandle, MESSAGE_CAPTURED_OK, IntPtr.Zero, IntPtr.Zero);
                    }
                    Thread.Sleep(100);
                }
            }
            catch { }

        }

        [DllImport("user32.dll", EntryPoint = "SendMessageA")]
        public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);


        private void bnIdentify_Click(object sender, EventArgs e)
        {
            if (!bIdentify)
            {
                bIdentify = true;
                DisplayMessage(MessageManager.msg_FP_PressForIdentification, true);
            }
        }

        private void bnVerify_Click(object sender, EventArgs e)
        {
            if (bIdentify)
            {
                bIdentify = false;
                btnVerify.Text = VerifyButtonToggle;
                DisplayMessage(MessageManager.msg_FP_PressForVerification, true);
            }
            else
            {
                bIdentify = true;
                btnVerify.Text = VerifyButtonDefault;
            }
        }


        protected override void DefWndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case MESSAGE_CAPTURED_OK:
                    {
                        parentForm.statusBar.Visible = false;
                        DisplayFingerPrintImage();

                        if (IsRegister)
                        {
                            #region -------- IF REGISTERED FINGERPRINT --------

                            int ret = zkfp.ZKFP_ERR_OK;
                            int fid = 0, score = 0;
                            ret = fpInstance.Identify(CapTmp, ref fid, ref score);
                            if (zkfp.ZKFP_ERR_OK == ret)
                            {
                                int deleteCode = fpInstance.DelRegTemplate(fid);   // <---- REMOVE FINGERPRINT
                                if (deleteCode != zkfp.ZKFP_ERR_OK)
                                {
                                    DisplayMessage(MessageManager.msg_FP_CurrentFingerAlreadyRegistered + fid, false);
                                    return;
                                }
                            }
                            if (RegisterCount > 0 && fpInstance.Match(CapTmp, RegTmps[RegisterCount - 1]) <= 0)
                            {
                                DisplayMessage("Please press the same finger " + REGISTER_FINGER_COUNT + " times for enrollment", true);

                                return;
                            }
                            Array.Copy(CapTmp, RegTmps[RegisterCount], cbCapTmp);


                            if (RegisterCount == 0) btnEnroll.Enabled = false;

                            RegisterCount++;
                            if (RegisterCount >= REGISTER_FINGER_COUNT)
                            {

                                RegisterCount = 0;
                                ret = GenerateRegisteredFingerPrint();   // <--- GENERATE FINGERPRINT TEMPLATE

                                if (zkfp.ZKFP_ERR_OK == ret)
                                {

                                    ret = AddTemplateToMemory();        //  <--- LOAD TEMPLATE TO MEMORY
                                    if (zkfp.ZKFP_ERR_OK == ret)         // <--- ENROLL SUCCESSFULL
                                    {
                                        string fingerPrintTemplate = string.Empty;
                                        zkfp.Blob2Base64String(RegTmp, regTempLen, ref fingerPrintTemplate);

                                        Utilities.EnableControls(true, btnVerify, btnIdentify);
                                        Utilities.EnableControls(false, btnEnroll);


                                        // GET THE TEMPLATE HERE : fingerPrintTemplate


                                        DisplayMessage(MessageManager.msg_FP_EnrollSuccessfull, true);

                                        DisconnectFingerPrintCounter();
                                    }
                                    else
                                        DisplayMessage(MessageManager.msg_FP_FailedToAddTemplate, false);

                                }
                                else
                                    DisplayMessage(MessageManager.msg_FP_UnableToEnrollCurrentUser + ret, false);

                                IsRegister = false;
                                return;
                            }
                            else
                            {
                                int remainingCont = REGISTER_FINGER_COUNT - RegisterCount;
                           
                                lblFingerPrintCount.Text = remainingCont.ToString();
                                string message = "Please provide your fingerprint " + remainingCont + " more time(s)";

                                DisplayMessage(message, true);
                                this.counHelper = remainingCont;

                            }
                            #endregion
                        }
                        else
                        {

                            #region ------- IF RANDOM FINGERPRINT -------
                            // If unidentified random fingerprint is applied

                            if (regTempLen <= 0)
                            {
                                DisplayMessage(MessageManager.msg_FP_UnidentifiedFingerPrint, false);
                                return;
                            }


                            if (bIdentify)
                            {
                                int ret = zkfp.ZKFP_ERR_OK;
                                int fid = 0, score = 0;
                                ret = fpInstance.Identify(CapTmp, ref fid, ref score);
                                if (zkfp.ZKFP_ERR_OK == ret)
                                {
                                    DisplayMessage(MessageManager.msg_FP_IdentificationSuccess + ret, true);
                                    return;
                                }
                                else
                                {
                                    DisplayMessage(MessageManager.msg_FP_IdentificationFailed + ret, false);
                                    return;
                                }
                            }
                            else
                            {
                                int ret = fpInstance.Match(CapTmp, RegTmp);
                                if (0 < ret)
                                {
                                    DisplayMessage(MessageManager.msg_FP_MatchSuccess + ret, true);
                                    return;
                                }
                                else
                                {
                                    DisplayMessage(MessageManager.msg_FP_MatchFailed + ret, false);
                                    return;
                                }
                            }
                            #endregion
                        }
                    }
                    break;

                default:
                    base.DefWndProc(ref m);
                    break;
            }
        }



        /// <summary>
        /// FREE RESOURCES
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bnFree_Click(object sender, EventArgs e)
        {
            textBox1.Text = "";
            textBox3.Text = "";
            label10.Text = "loading";
            label16.Text = "loading";
            label15.Text = "loading";
            label14.Text = "loading";
            label13.Text = "loading";
            label12.Text = "loading";
            label11.Text = "loading";
            label17.Text = "loading";
            label21.Text = "loading";
            label23.Text = "loading";
            this.freeRes = true; 


            int result = fpInstance.Finalize();

            if (result == zkfp.ZKFP_ERR_OK)
            {
                DisconnectFingerPrintCounter();
                IsRegister = false;
                RegisterCount = 0;
                regTempLen = 0;
                ClearImage();
                cmbIdx.Items.Clear();
                Utilities.EnableControls(true, btnInit);
                Utilities.EnableControls(false, btnFree, btnClose, btnEnroll, btnVerify, btnIdentify);

                DisplayMessage("Resources were successfully released from the memory !!", true);
            }
            else
                DisplayMessage("Failed to release the resources !!", false);
        }

        private void ClearImage()
        {
            picFPImg.Image = null;
            //pbxImage2.Image = null;
        }

        private void bnEnroll_Click(object sender, EventArgs e)
        {
            if (!IsRegister)
            {
                ClearImage();
                IsRegister = true;
                RegisterCount = 0;
                regTempLen = 0;
                Utilities.EnableControls(false, btnEnroll, btnVerify, btnIdentify);
                DisplayMessage("Please press your finger " + REGISTER_FINGER_COUNT + " times to register", true);

                lblFingerPrintCount.Visible = true;
               // lblFingerPrintCount.Text = REGISTER_FINGER_COUNT.ToString();
            }
        }




        public object PushToDevice(object args)
        {
            DisplayMessage("Pushed to fingerprint !", true);
            return null;
        }


        public void ReEnrollUser(bool enableEnroll, bool clearDeviceUser = true)
        {
            ClearImage();
            if (clearDeviceUser && !btnInit.Enabled) ClearDeviceUser();
            if (enableEnroll) btnEnroll.Enabled = true;
        }


        public void ClearDeviceUser()
        {
            try
            {
                int deleteCode = fpInstance.DelRegTemplate(iFid);   // <---- REMOVE FINGERPRINT
                if (deleteCode != zkfp.ZKFP_ERR_OK)
                {
                    DisplayMessage(MessageManager.msg_FP_UnableToDeleteFingerPrint + iFid, false);
                }
                iFid = 1;
            }
            catch { }

        }


        public bool ReleaseResources()
        {
            try
            {
                ReEnrollUser(true, true);
                bnClose_Click(null, null);
                return true;
            }
            catch
            {
                return false;
            }

        }
        private bool OpenConnection()
        {
            try
            {
                cnn.Open();
                return true;
            }
            catch (MySqlException ex)
            {
       

                switch (ex.Number)
                {
                    case 0:
                        MessageBox.Show("Cannot connect to server.  Contact administrator");
                        break;

                    case 1045:
                        MessageBox.Show("Invalid username/password, please try again");
                        break;
                }
                return false;
            }
        }

        #region -------- CONNECT/DISCONNECT DEVICE --------



        /// <summary>
        /// DISCONNECT DEVICE
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bnClose_Click(object sender, EventArgs e)
        {
            OnDisconnect();
        }


        public void OnDisconnect()
        {
            bIsTimeToDie = true;
            RegisterCount = 0;
            DisconnectFingerPrintCounter();
            ClearImage();
            Thread.Sleep(1000);
            int result = fpInstance.CloseDevice();

            captureThread.Abort();
            if (result == zkfp.ZKFP_ERR_OK)
            {
                Utilities.EnableControls(false, btnInit, btnClose, btnEnroll, btnVerify, btnIdentify);

                lblDeviceStatus.Text = Disconnected;

                Thread.Sleep(1000);
                result = fpInstance.Finalize();   // CLEAR RESOURCES

                if (result == zkfp.ZKFP_ERR_OK)
                {
                    regTempLen = 0;
                    IsRegister = false;
                    cmbIdx.Items.Clear();
                    Utilities.EnableControls(true, btnInit);
                    Utilities.EnableControls(false, btnClose, btnEnroll, btnVerify, btnIdentify);

                    ReInitializeInstance();

                    DisplayMessage(MessageManager.msg_FP_Disconnected, true);
                }
                else
                    DisplayMessage(MessageManager.msg_FP_FailedToReleaseResources, false);


            }
            else
            {
                string errorMessage = FingerPrintDeviceUtilities.DisplayDeviceErrorByCode(result);
                DisplayMessage(errorMessage, false);
            }
        }


        #endregion



        #region ------- COMMON --------

        private void FingerPrintControl_Load(object sender, EventArgs e) { FormHandle = this.Handle; }

        private void ReInitializeInstance()
        {
            Utilities.EnableControls(true, btnInit);
            Utilities.EnableControls(false, btnClose, btnEnroll, btnVerify, btnIdentify);
            DisconnectFingerPrintCounter();
            bIdentify = true;
            btnVerify.Text = VerifyButtonDefault;
        }

        private void DisconnectFingerPrintCounter()
        {
            lblFingerPrintCount.Text = REGISTER_FINGER_COUNT.ToString();
            lblFingerPrintCount.Visible = false;
        }

        #endregion


        #region -------- UTILITIES --------


        /// <summary>
        /// Combines Three Pre-Registered Fingerprint Templates as One Registered Fingerprint Template
        /// </summary>
        /// <returns></returns>
        private int GenerateRegisteredFingerPrint()
        {
            return fpInstance.GenerateRegTemplate(RegTmps[0], RegTmps[1], RegTmps[2], RegTmp, ref regTempLen);
        }

        /// <summary>
        /// Add A Registered Fingerprint Template To Memory | params: (FingerPrint ID, Registered Template)
        /// </summary>
        /// <returns></returns>
        private int AddTemplateToMemory()
        {
            return fpInstance.AddRegTemplate(iFid, RegTmp);
        }




        private void DisplayFingerPrintImage()
        {
            // NORMAL METHOD >>>

            //Bitmap fingerPrintImage = Utilities.GetImage(FPBuffer, fpInstance.imageWidth, fpInstance.imageHeight);
            //Rectangle cropRect = new Rectangle(0, 0, pbxImage2.Width / 2, pbxImage2.Height / 2);
            //Bitmap target = new Bitmap(cropRect.Width, cropRect.Height);
            //using (Graphics g = Graphics.FromImage(target))
            //{
            //    g.DrawImage(fingerPrintImage, new Rectangle(0, 0, target.Width, target.Height), cropRect, GraphicsUnit.Pixel);
            //}
            //this.pbxImage2.Image = target;



            // OPTIMIZED METHOD
            MemoryStream ms = new MemoryStream();
            BitmapFormat.GetBitmap(FPBuffer, mfpWidth, mfpHeight, ref ms);
            Bitmap bmp = new Bitmap(ms);
            this.obj = bmp; 
            
            this.picFPImg.Image = bmp;

        }

        private void DisplayMessage(string message, bool normalMessage)
        {
            try
            {
                Utilities.ShowStatusBar(message, parentForm.statusBar, normalMessage);
            }
            catch (Exception ex)
            {
                Utilities.ShowStatusBar(ex.Message, parentForm.statusBar, false);
            }
        }

        public static bool CheckForInternetConnection(int timeoutMs = 10000, string url = null)
        {
            try
            {
                Ping myPing = new Ping();
                String host = "google.com";
                byte[] buffer = new byte[32];
                int timeout = 1000;
                PingOptions pingOptions = new PingOptions();
                PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);
                return (reply.Status == IPStatus.Success);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static double CalculateBMI(double weight, double height)
        {
            return (weight / (height * height));
        }

        #endregion

        private async void button1_Click(object sender, EventArgs e) {

            


          
     
            if (!CheckForInternetConnection())
            {
                MessageBox.Show("Please check you connection and try again");
                return;
            }

            if (comboBox1.SelectedIndex == -1)
            {
                MessageBox.Show("Please select the company");
                return;
            }
            String input = textBox1.Text.Replace(" ", "");
            int  input2; 
            if (input == "")
            {
                MessageBox.Show("Please enter the ID number");
                return;
            }

            try
            {
                 input2   = int.Parse(input);

            }
            catch
            {
                MessageBox.Show("invaled input");
                return;

            }

            




            try
            {
                string x = await GetData("", input2);


                if (x == null) {
                    MessageBox.Show("the id cant be found ");
                    
                }


                var dynamicResult = JsonConvert.DeserializeObject<dynamic>(x);
                var dd= ""; 
               // Console.WriteLine(dynamicResult);
                int count = dynamicResult.response.count;
                //  Console.WriteLine(dynamicResult.response.results[0].toString());

                /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                bool v  = true ;
                int j = 0; 
                for (int i = 0; i < count; i ++ )
                {

                    if (this.CompanyID == dynamicResult.response.results[i]["company"].ToString())
                    {
                        // do somthis 
                        MessageBox.Show("found");
                        Console.WriteLine(dynamicResult.response.results.ToString());
                        v = false;
                        j = i;
                        break;


                    }


                }
                if (v)
                {
                    try
                    {
                        MessageBox.Show("there is no such an employee in this company");
                        textBox1.Text = "";
                        textBox3.Text = "";
                        label10.Text = "loading";
                        label16.Text = "loading";
                        label15.Text = "loading";
                        label14.Text = "loading";
                        label13.Text = "loading";
                        label12.Text = "loading";
                        label11.Text = "loading";
                        label17.Text = "loading";
                        label21.Text = "loading";
                        label23.Text = "loading";
                        textBox2.Text = "loading";
                        textBox4.Text = "loading";
                        textBox5.Text = "loading";

                        ClearImage();
                        return;
                    }
                    catch (Exception asa)
                    {
                        MessageBox.Show(asa.Message);
                        return;

                    }
                }
               

                //////////////////////////////////////////////////////////////////////////////////////////////////////////////

                if (count <= 0)
                {
                    MessageBox.Show("Cant find the ID plz make sure of it and try again ");
                }
                else                 //gender
                {

                    //  MessageBox.Show(dynamicResult.response.results[j]["gender"].ToString());
                    // return;

                    //Console.WriteLine(dynamicResult.response.results[j]["gender"]);

                    label10.Text = dynamicResult.response.results[j]["empid"] == null ? "Not found" : label10.Text = dynamicResult.response.results[j]["empid"];

                    label16.Text = dynamicResult.response.results[j]["name en"] == null ? "Not found" : label16.Text = dynamicResult.response.results[j]["name en"];
                    
                    label15.Text = dynamicResult.response.results[j]["gender"] == null ? "Not found" : label15.Text = dynamicResult.response.results[j]["gender"];
                    
                    label14.Text = dynamicResult.response.results[j]["nationality"] == null ? "Not found" : label14.Text = dynamicResult.response.results[j]["nationality"];
                    
                    label13.Text = dynamicResult.response.results[j]["course"] == null ? "Not found" : label13.Text = dynamicResult.response.results[j]["course"];
                    
                    label12.Text = dynamicResult.response.results[j]["empid"] == null ? "Not found" : label12.Text = dynamicResult.response.results[j]["empid"];
                    
                    label11.Text = dynamicResult.response.results[j]["height"] == null ? "Not found" : label11.Text = dynamicResult.response.results[j]["height"];
                    
                    label17.Text = dynamicResult.response.results[j]["weight"] == null ? "Not found" : label17.Text = dynamicResult.response.results[j]["weight"];
                    
                    label23.Text = dynamicResult.response.results[j]["dob"] == null ? "Not found" : label23.Text = dynamicResult.response.results[j]["dob"];
                  
                    textBox2.Text = label11.Text;
                    textBox4.Text = label17.Text;
                    textBox6.Text = label15.Text;


                    if (label17.Text != null || label11.Text != null)
                    {
                        double h = double.Parse(label11.Text);
                        double w = double.Parse(label17.Text);
                        label21.Text = (CalculateBMI(w,h) / 1000).ToString().Substring(0,4);

                        textBox5.Text = (CalculateBMI(w, h) / 1000).ToString().Substring(0, 4);

                    }
                    else
                    {
                        label21.Text ="Not found 404";

                    }
                   

  


                    MessageBox.Show("Data have been loaded successfully");
           
                }



                
            }
            catch(Exception ee)
            {
 
                Console.WriteLine(ee.Message + "  ErRorr  ");
                MessageBox.Show(ee.Message + "  ErRorr  ");
            }


        }


    


        private void picFPImg_Click(object sender, EventArgs e)
        {}





        public static async Task<string> GetData(string url, int param1)
        {
            string content = null;
            /*url = "https://www.tsti.ae/version-test/api/1.1/obj/new_request/?constraints=[ { \"key\": \"empid\", \"constraint_type\": \"equals\", \"value\": \""  +param1+ "\" }]";
            ;
            */

            url = "https://www.tsti.ae/version-live/api/1.1/obj/new_request/?constraints=[ { \"key\": \"empid\", \"constraint_type\": \"equals\", \"value\": \"" + param1 + "\" }]";
            ;

            var client = new HttpClient();
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("het alomost there ");
                return await response.Content.ReadAsStringAsync();
            }

            return null;
        }



        public static async Task<string> GetCompanyData()
        {
            string content = null;
  
            String url = "https://www.tsti.ae/version-test/api/1.1/obj/companies";

            var client = new HttpClient();
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
               
                return await response.Content.ReadAsStringAsync();
            }

            return null;
        }
        bool initComboBox = false;
        public async Task initComboBoxItems()

        {
            if (initComboBox)
            {
                MessageBox.Show("you already initialized the list ");
                return;
            }
            if (!CheckForInternetConnection())
            {
                MessageBox.Show("Please check you connection and try again");
                return;
            }
            try
            {
                var x = await GetCompanyData();
                var dynamicResult2 = JsonConvert.DeserializeObject<dynamic>(x);
                Dictionary<string, string> dict = new Dictionary<string, string>();
                int count = dynamicResult2.response.count;
                var res = dynamicResult2.response.results;


                for (int i = 0; i< count; i++)
                {
                    String v1 = dynamicResult2.response.results[i]["_id"];
                    String v2 = dynamicResult2.response.results[i]["name"];

                    // Console.WriteLine(dynamicResult2.response.results[0]["name"]);
                    dict.Add(v1,v2);
                    comboBox1.Items.Add(v2);
                    
                    
                }








                // Console.WriteLine(dynamicResult);
                // String help = dynamicResult2.response.results[1]["_id"];
                //   Console.WriteLine(dict.Values.ToString());
                //MessageBox.Show(dict.Values.ToString());
                MessageBox.Show("the company list initialized successfully");
                this.comboBox1.SelectedIndexChanged+= new System.EventHandler(comboBox1_SelectedIndexChanged_1);
                this.selected_company = dict;

                initComboBox = true; 
               
         


            }
            catch (Exception ee)
            {
                Console.WriteLine(ee.Message);
                MessageBox.Show("error-->" , ee.Message);
            }
            
            

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
         

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label18_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)

            
        {
            String imageName = this.label10.Text;
            if (textBox1.Text.Replace(" " ,"") == "" )
            {
                MessageBox.Show("Please enter the id ");
                return;
            }
            if(textBox3 .Text.Replace(" " , "") == "")
            {
                MessageBox.Show("Please enter the blood Pressure");
                return;
            }
            if (!this.isInit)
            {
                MessageBox.Show("Please click on initialze buty swap you finger 3 times and try again ");
                return;
                
            }
            if (this.RegisterCount == -1 )
            {
                MessageBox.Show("Please click enrolle and try again");
                return;
                
            }

            if (this.freeRes)
            {
                MessageBox.Show("Please Initialize  the device again");
                this.freeRes = false;
                return;

            }

            if (picFPImg.Image == null)
            {
                MessageBox.Show(" PLease scan you finger ");
                return;

            }
            if (label10.Text =="loading" || label10.Text == "Loading")
            {
                MessageBox.Show("Plese click search");
                return;

            }

            try
            {

                using (Bitmap bmp = new Bitmap(picFPImg.ClientSize.Width,
                             picFPImg.ClientSize.Height))
                {

                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                    // Create a new directory on the desktop
                   
                    string folderName = "PICS";
                    string pathString = Path.Combine(desktopPath, folderName);
                    Directory.CreateDirectory(pathString);
                    pathString = Path.Combine(desktopPath, folderName);
                 
                    string imagePath = Path.Combine(pathString, label10.Text +"_"+comboBox1.SelectedItem.ToString());
                    this.pathP = Path.Combine(pathString, label10.Text + "_" + comboBox1.SelectedItem.ToString());
                    picFPImg.DrawToBitmap(bmp, picFPImg.ClientRectangle);
                    bmp.Save(imagePath + ".jpg");
                }

                  DateTime now = DateTime.Now;
                label25.Text = now.ToString();
                MessageBox.Show("Captured successfully");
                this.capther = true; 


            }
            catch (Exception er )
            {
                MessageBox.Show(er.Message);
            }


          //  MessageBox.Show("DONE");

           // MessageBox.Show(this.regTempLen.ToString() + "<== regTempLen");
           // MessageBox.Show(this.RegisterCount.ToString() + "<== RegisterCount");
        }

        

        private void lblFingerPrintCount_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
           
        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            textBox1.Text = "";
            textBox3.Text = "";
            textBox6.Text = "";
            label10.Text = "loading";
            label16.Text = "loading";
            label15.Text = "loading";
            label14.Text = "loading";
            label13.Text = "loading";
            label12.Text = "loading";
            label11.Text = "loading";
            label17.Text = "loading";
            label21.Text = "loading";
            label23.Text = "loading";
            if (!isInit)
            {
                MessageBox.Show("you have not strated yet");



            }
            else
            {
                ClearImage();
                //ReInitializeInstance();
                disconeectd();
                init1();
            }



        }




        public void init1() {


            {
                parentForm.statusBar.Visible = false;
                cmbIdx.Items.Clear();

                int initializeCallBackCode = fpInstance.Initialize();
                if (zkfp.ZKFP_ERR_OK == initializeCallBackCode)
                {
                    int nCount = fpInstance.GetDeviceCount();
                    if (nCount > 0)
                    {
                        for (int i = 1; i <= nCount; i++) cmbIdx.Items.Add(i.ToString());

                        cmbIdx.SelectedIndex = 0;
                        btnInit.Enabled = false;

                        DisplayMessage(MessageManager.msg_FP_InitComplete, true);
                    }
                    else
                    {
                        int finalizeCount = fpInstance.Finalize();
                        DisplayMessage(MessageManager.msg_FP_NotConnected, false);
                    }




                    // CONNECT DEVICE

                    #region -------- CONNECT DEVICE --------

                    int openDeviceCallBackCode = fpInstance.OpenDevice(cmbIdx.SelectedIndex);
                    if (zkfp.ZKFP_ERR_OK != openDeviceCallBackCode)
                    {
                        DisplayMessage($"Uable to connect with the device! (Return Code: {openDeviceCallBackCode} )", false);
                        return;
                    }

                    Utilities.EnableControls(false, btnInit);
                    Utilities.EnableControls(true, btnClose, btnEnroll, btnVerify, btnIdentify, btnFree);

                    //  RegisterCount = 0;
                    regTempLen = 0;
                    iFid = 1;

                    //for (int i = 0; i < 3; i++)
                    for (int i = 0; i < REGISTER_FINGER_COUNT; i++)
                    {
                        RegTmps[i] = new byte[2048];
                    }

                    byte[] paramValue = new byte[4];
                    int size = 4;

                    //fpInstance.GetParameters

                    fpInstance.GetParameters(1, paramValue, ref size);
                    zkfp2.ByteArray2Int(paramValue, ref mfpWidth);

                    size = 4;
                    fpInstance.GetParameters(2, paramValue, ref size);
                    zkfp2.ByteArray2Int(paramValue, ref mfpHeight);

                    FPBuffer = new byte[mfpWidth * mfpHeight];

                    //FPBuffer = new byte[fpInstance.imageWidth * fpInstance.imageHeight];

                    captureThread = new Thread(new ThreadStart(DoCapture));
                    captureThread.IsBackground = true;
                    captureThread.Start();


                    bIsTimeToDie = false;

                    string devSN = fpInstance.devSn;
                    lblDeviceStatus.Text = "Connected \nDevice S.No: " + devSN;

                    DisplayMessage("You are now connected to the device.", true);

                    this.isInit = true;
                    this.registerCountHeper = RegisterCount;



                    #endregion

                }
                else
                    DisplayMessage("Unable to initailize the device. Make sure the device is connected " + FingerPrintDeviceUtilities.DisplayDeviceErrorByCode(initializeCallBackCode) + " !! ", false);

            }



        }
        public void disconeectd() {
            OnDisconnect();
        }
        //ComboBox comboBox1 = new ComboBox();

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //comboBox1.Items.Add("llll");

        }

        private void comboBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            try
            {
                string conpanyId = null;
                String name = comboBox1.SelectedItem.ToString();
                foreach (KeyValuePair<string, String> i in this.selected_company)
                {
                    if (name == i.Value)
                    {
                        this.CompanyID = i.Key;
                        break;
                    }
                   // Console.WriteLine( i.Key + " ------------  "+ i.Value);
                }
                //MessageBox.Show(this.selected_company[name]);
                // MessageBox.Show(this.selected_company.Values.ToString());
            }

            catch (Exception ee1)
            {
                MessageBox.Show("error " + " " + ee1.Message);

            }
        }

        private async void  button5_Click(object sender, EventArgs e)
        {
            initComboBoxItems();
  
        }

        private void button4_Click(object sender, EventArgs e)
        {
            String h = textBox2.Text.Replace(" ", "");
            String w = textBox4.Text.Replace(" ", "");
           
            int inputH;
            int inputW;

            if (h == "")
            {
                MessageBox.Show("Please enter the height ");
                return;
            }
            if (w == "")
            {
                MessageBox.Show("Please enter the weight ");
                return;
            }

            try
            {
                inputH = int.Parse(h);
                inputW = int.Parse(w);
                textBox5.Text = (CalculateBMI(inputW, inputH) / 1000).ToString().Substring(0, 4);


            }
            catch
            {
                MessageBox.Show("invaled input");
                return;

            }

        }

        private void label21_Click(object sender, EventArgs e)
        {

        }

        private void button6_Click(object sender, EventArgs e)
        {
            /*
             
              
               string folderName = "PICS";
                    string pathString = Path.Combine(desktopPath, folderName);
                    Directory.CreateDirectory(pathString);
                    pathString = Path.Combine(desktopPath, folderName);

                    string imagePath = Path.Combine(pathString, label10.Text);

                    picFPImg.DrawToBitmap(bmp, picFPImg.ClientRectangle);
                    bmp.Save(imagePath + ".jpg");

             */
            if (!this.capther)
            {
                MessageBox.Show("Please click capture first and try again");
                return;

            }
            try
            {


                // --String path = Path.Combine(Directory.GetParent(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName).FullName, "d.docx");
                // String path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "template.docx");
                //--  string resourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
                // string path = Path.Combine(resourcePath, "d.docx");
                // String path = Path.Combine(Directory.GetParent(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName).FullName, "Resources\\d.docx");

                string p1 = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                String path = Path.Combine(p1, "d.docx");

                var wordApp = new Word.Application(); //Application();
                var wordDoc = wordApp.Documents.Add(path);


 






                    //{ImagePlaceholder}
                    //    var wordDoc = wordApp.Documents.Add(@"d.docx");

                    wordDoc.Content.Find.Execute(FindText: "{course}", ReplaceWith: label13.Text);
                    wordDoc.Content.Find.Execute(FindText: "{name}", ReplaceWith: label16.Text);
                    wordDoc.Content.Find.Execute(FindText: "{date}", ReplaceWith: label25.Text);
                    wordDoc.Content.Find.Execute(FindText: "{company}", ReplaceWith: comboBox1.SelectedItem.ToString());
                    wordDoc.Content.Find.Execute(FindText: "{id}", ReplaceWith: label10.Text);
                    wordDoc.Content.Find.Execute(FindText: "{height}", ReplaceWith: textBox2.Text);
                    wordDoc.Content.Find.Execute(FindText: "{weight}", ReplaceWith: textBox4.Text);
                    wordDoc.Content.Find.Execute(FindText: "{bp}", ReplaceWith: textBox3.Text);
                    wordDoc.Content.Find.Execute(FindText: "{na}", ReplaceWith: label14.Text);
                    wordDoc.Content.Find.Execute(FindText: "{gen}", ReplaceWith: textBox6.Text);
                    wordDoc.Content.Find.Execute(FindText: "{dob}", ReplaceWith: label23.Text);
                    wordDoc.Content.Find.Execute(FindText: "{bmi}", ReplaceWith: textBox5.Text);


                //////////////////////////////////////////////////////////////////////////////////////////////

                
                    string company = this.CompanyID;
                    string empid = label10.Text;

                    string input = company + empid;
                    byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                    byte[] hashBytes;

                    using (SHA256 hash = SHA256.Create())
                    {
                        hashBytes = hash.ComputeHash(inputBytes);
                    }

                    int uniqueId = Math.Abs(BitConverter.ToInt32(hashBytes, 0)) % 100000000;




                    wordDoc.Content.Find.Execute(FindText: "{ref}", ReplaceWith: uniqueId);





                ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                var shape = wordDoc.Shapes.AddPicture(
                        FileName: this.pathP + ".jpg",
                        LinkToFile: false,
                        SaveWithDocument: true,
                        Left: wordApp.InchesToPoints(4.34f),
                        Top: wordApp.InchesToPoints(8.20f),
                        Width: wordApp.InchesToPoints(1),
                        Height: wordApp.InchesToPoints(1)
                    );




                //  String pp = this.pathP + ".docx";

                //wordDoc.SaveAs(pp);
                //      this.WindowState = FormWindowState.Minimized;
              //  FormWindowState.Minimized; 
          
                wordDoc.SaveAs2(this.pathP + ".pdf", Word.WdSaveFormat.wdFormatPDF);
                wordDoc.Close();
                wordApp.Quit();
                MessageBox.Show("Saved successfully");



                
                this.capther = false;


                textBox1.Text = "";
                textBox3.Text = "";
                label10.Text = "loading";
                label16.Text = "loading";
                label15.Text = "loading";
                label14.Text = "loading";
                label13.Text = "loading";
                label12.Text = "loading";
                label11.Text = "loading";
                label17.Text = "loading";
                label21.Text = "loading";
                label23.Text = "loading";
                textBox2.Text = "loading";
                textBox4.Text = "loading";
                textBox5.Text = "loading";
                textBox6.Text = "loading";

                ClearImage();

              

            }
            catch (Exception e3)
            {
                MessageBox.Show("error  " + e3.Message);
                this.capther = false;

            }
        }

        private void label15_Click(object sender, EventArgs e)
        {

        }

        private void button7_Click(object sender, EventArgs e)
        {
            string p1 = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            String path = Path.Combine(p1, "d.docx");

            Console.WriteLine(path);
            MessageBox.Show(path);
 
        }

        private void button7_Click_1(object sender, EventArgs e)
        {
            string currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string path = Path.Combine(currentDirectory, "d.docx");


            Console.WriteLine(path);
            MessageBox.Show(path);


        }
    }
        


    }

