using Dofe_Re_Entry.UserControls.DeviceController;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using ZKTecoFingerPrintScanner_Implementation.Helpers;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using System.Drawing.Imaging;
using MessageBox = System.Windows.Forms.MessageBox;
//using Word = Microsoft.Office.Interop.Word;
using System;

namespace ZKTecoFingerPrintScanner_Implementation
{
    public partial class Master : Form
      
    {
        FingerPrintControl fingerPrintControl = null;
        public Master()
        {
      
            InitializeComponent();
            pnlStage.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.ClientSize = Screen.PrimaryScreen.WorkingArea.Size;
            
            
             //FormWindowState.Maximized = true;
            //
            int ourScreenWidth = Screen.FromControl(this).WorkingArea.Width;
            int ourScreenHeight = Screen.FromControl(this).WorkingArea.Height;
            float scaleFactorWidth = (float)ourScreenWidth / 1600f;
            float scaleFactorHeigth = (float)ourScreenHeight / 900f;
            SizeF scaleFactor = new SizeF(scaleFactorWidth, scaleFactorHeigth);
            Scale(scaleFactor);


            this.WindowState = FormWindowState.Maximized;


            //


            //
            // Form form = new Form();
            //

            Panel panel1 = new Panel();
            panel1.Dock = DockStyle.Fill;
            panel1.AutoScroll = true;
            this.Controls.Add(panel1);

          //  form.Controls.Add(panel1);

            //form.WindowState = FormWindowState.Maximized;





            LoadFingerprintControl();
       

        }

   

        /// <summary>
        /// Before closing the application clear all the resources used by the application
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            try
            {
                
                if (fingerPrintControl != null)
                {
                    if (fingerPrintControl.cmbIdx.Items.Count > 0)
                        fingerPrintControl.OnDisconnect();
                }
            }
            catch
            { }
        }

        private void LoadFingerprintControl()
        {
            fingerPrintControl = new FingerPrintControl();
            fingerPrintControl.parentForm = this;
            fingerPrintControl.Dock = DockStyle.Fill;
            pnlStage.Controls.Add(fingerPrintControl);
        }


        private void panel2_Paint(object sender, PaintEventArgs e)
        {
            int y = panel2.Height;
            GraphicsManager.DrawLine(panel2, Color.LightGray, 0, y, panel2.Width, y, 2);
        }

        private void lblHeader_Click(object sender, System.EventArgs e)
        {

        }

        private void label2_Click(object sender, System.EventArgs e)
        {

        }

        private void label1_Click(object sender, System.EventArgs e)
        {

        }

        private void flowLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void Master_Load(object sender, System.EventArgs e)
        {

        }

        private void toolStripContainer1_ContentPanel_Load(object sender, System.EventArgs e)
        {

        }

        private void label1_Click_1(object sender, System.EventArgs e)
        {

        }

        private void label4_Click(object sender, System.EventArgs e)
        {

        }

        private void label6_Click(object sender, System.EventArgs e)
        {

        }

        private void label10_Click(object sender, System.EventArgs e)
        {

        }

        public void textBox8_TextChanged(object sender, System.EventArgs e)
        {
         
        }

        private void statusBar_Load(object sender, System.EventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, System.EventArgs e)
        {

        }

        private void button1_Click(object sender, System.EventArgs e)
        {

            Rectangle rect = Screen.GetBounds(Point.Empty);
            using (Bitmap bitmap = new Bitmap(Screen.FromControl(this).WorkingArea.Width, Screen.FromControl(this).WorkingArea.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, rect.Size);
                }
                // select the save location of the captured screenshot
                bitmap.Save(@"C:\Users\Louie\Desktop\momo\apture.jpg", ImageFormat.Png);

                // show a message to let the user know that a screenshot has been captured
                MessageBox.Show("Screenshot taken! Press `OK` to continue...");
            }

        }

        private void pnlStage_Paint(object sender, PaintEventArgs e)
        {

        }

        private void pictureBox3_Click(object sender, System.EventArgs e)
        {

        }

        private void pictureBox2_Click(object sender, System.EventArgs e)
        {

        }

        private void button1_Click_1(object sender, System.EventArgs e)
        {
           
           
        }

        private void button1_Click_2(object sender, EventArgs e)
        {
            Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory.ToString());
        }
    }
}
