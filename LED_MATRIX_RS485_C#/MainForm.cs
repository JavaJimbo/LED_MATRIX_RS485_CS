/* LED_MATRIX_RS485_CS
 * 
 * 3-13-19 
 * 3-14-19: 
 * 3-17-19
 * 3-18-19: Draws test circles, skips points when circle is divided into 360 invrements. 
 *          Got nice bold colors with 1440 0.25 degree increments
 *          Target animation works nicely.
 * 3-23-19: Added random colors and widths to target animation
 * 3-24-19: Got image rotation working.
 * 3-31-19: 
 * 04-06-19: Got six serial ports working with all twenty panels!
 * 04-15-19: Got RS485 communication working. 
 * 04-16-19: Works with panels but color translation looks lousy.
 * 04-20-19: 
 * 04-22-19: Works with both 16x32 and 32x32 LED panels, using RS485
 *            Targets work, but lines appear in black rings. Not doing anything with rotating images yet.
 * 04-23-19: Got rid of unnecessary serial port stuff.
 * 04-24-19: TEST button manually creates white rings of increasing size. 
 *           Line errors for some reason aren't appearing, but still happen in target loop.
 * 04-28-19: Fixed bugs in Target code. Works great with 8 bit color on matrix boards.
 */

#define USE_SERIAL

using Microsoft.Win32;
using System;
using System.Timers;
using System.Drawing;
using System.IO.Ports;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

using COMPortTerminal.Properties;
using PanelSpace;

namespace COMPortTerminal
{
    [StructLayout(LayoutKind.Explicit)]
    struct convertType
    {
        [FieldOffset(0)]
        public Byte byte0;

        [FieldOffset(1)]
        public Byte byte1;

        [FieldOffset(2)]
        public Byte byte2;

        [FieldOffset(3)]
        public Byte byte3;

        [FieldOffset(0)]
        public UInt16 shtInteger;

        [FieldOffset(0)]
        public UInt32 lngInteger;
    };

    enum Mode : int
    {
        STANDBY = 0,
        RUN_TARGET,
        RUN_ROTATE
    }



    public partial class MainForm
    {
        public const int MAXPACKETSIZE = (LEDPanel.PANELSIZE * 4);
        public const int NUMCHANNELS = 3;
        public const int COL_OFFSET = 0;
        public const int PIXELSIZE = 4;
        public const int SIDE_MARGIN = 40;
        public const int TOP_MARGIN = 40;
        public const int CX_OFFSET = 100;
        public const int CY_OFFSET = 100;
        public const double PI = 3.14159265358979;
        public const double TORADIANS = (PI / 180);
        public const double TODEGREES = (180 / PI);
        public const int MAXRADIUS = 100;
        public const int MATRIX_WIDTH = MAXRADIUS * 2;
        public const int MATRIX_HEIGHT = MAXRADIUS * 2;
        public const int MATRIX_DATASIZE = (MATRIX_WIDTH * MATRIX_HEIGHT * NUMCHANNELS);
        public const int MAX_X = MAXRADIUS - 1;
        public const int MAX_Y = MAXRADIUS - 1;
        public const int X_OFFSET = MAXRADIUS;
        public const int Y_OFFSET = MAXRADIUS;
        public const int BITMAP_DATA_OFFSET = 54;
        public const int BITMAP_HEADER_SIZE = 54;
        public const int BITMAP_WIDTH = 800;
        public const int BITMAP_HEIGHT = 800;
        public const int BITMAP_TOTAL_SIZE = (BITMAP_WIDTH * BITMAP_HEIGHT * 3) + BITMAP_HEADER_SIZE;
        public const int BITMAP_DATA_SIZE = (BITMAP_WIDTH * BITMAP_HEIGHT * 3);
        public const int MAX_ANGLE = (360 * 4);
        public const double ANGLE_INCREMENT = (double) 0.25;
        public const int RING_WIDTH = 5;
        public int colorIndex = 0;

        int StartRadius = 6;
        int StopRadius = 7;
        public bool[] colorUsed = new bool[LEDPanel.MAXCOLOR];
        public int[] RingWidth = new int[LEDPanel.MAXCOLOR];
        public bool[] NewColor = new bool[LEDPanel.MAXCOLOR];
        public Ring[] Target;
        public int TargetColorIndex = 0;
        public int FirstRadius = 0;
        public int BitmapSize = 0;
        public int OpMode = (int)Mode.STANDBY;
        Byte[] NewBitmapData = new Byte[BITMAP_TOTAL_SIZE];
        public int TestAngle = 0;
        public int PreviousAngle = 0;
        bool SerialPortsOpen = false;        
        public byte ColorTestIndex = 0;
        public int PanelTestIndex = 0;
        public int RadiusTestIndex = 0;

        public byte[,] arrPanelInit = {
                    {1, 0, LEDPanel.HORIZONTAL, 64, 0}, {1, 1, LEDPanel.HORIZONTAL, 80, 0 }, {1, 2, LEDPanel.HORIZONTAL, 64, 32 },{1, 3, LEDPanel.HORIZONTAL, 80, 32},
                    {2, 0, LEDPanel.HORIZONTAL, 64, 64}, {2, 1, LEDPanel.HORIZONTAL, 80, 64}, {2, 2, LEDPanel.HORIZONTAL, 64, 96},{2, 3, LEDPanel.HORIZONTAL, 80, 96},
                    {3, 0, LEDPanel.HORIZONTAL, 32, 0}, {3, 1, LEDPanel.HORIZONTAL, 48, 0 }, {3, 2, LEDPanel.HORIZONTAL, 32, 32 },{3, 3, LEDPanel.HORIZONTAL, 48, 32},
                    {4, 0, LEDPanel.HORIZONTAL, 32, 64}, {4, 1, LEDPanel.HORIZONTAL, 48, 64}, {4, 2, LEDPanel.HORIZONTAL, 32, 96},{4, 3, LEDPanel.HORIZONTAL, 48, 96},
                    {5, 0, LEDPanel.HORIZONTAL, 0, 0}, {5, 1, LEDPanel.HORIZONTAL, 16, 0},{5, 2, LEDPanel.HORIZONTAL, 0, 32}, {5, 3, LEDPanel.HORIZONTAL, 16, 32},
                    {6, 0, LEDPanel.HORIZONTAL, 0, 64}, {6, 1, LEDPanel.HORIZONTAL, 16, 64},{6, 2, LEDPanel.HORIZONTAL, 0, 96}, {6, 3, LEDPanel.HORIZONTAL, 16, 96},
                    };

        LEDPanel[] MyPanels = new LEDPanel[LEDPanel.NUMPANELS];
        CRC MyCRC = new CRC();
        
        const byte STX = (byte)'>';
        const byte DLE = (byte)'/';
        const byte ETX = (byte)'\r';

        public const int MAXDATABYTES = (LEDPanel.PANELROWS * LEDPanel.PANELCOLS * 4);  // $$$$
        public const int MAXPACKET = (MAXDATABYTES * 4);

        convertType convertToInteger;

        public Byte[] bitmapData;

        public PolarPointXY[,] PolarMatrix, RotateMatrix;
        public XYMatrixPoint[,] XYmatrix;
        
        Bitmap Maribmp = (Bitmap)Bitmap.FromFile("C:\\Temp\\Marilyn.bmp");
        MemoryStream ms = new MemoryStream();
        Bitmap bmpDisplay;
        public Byte[] BitmapHeader;
        public bool EnableDiagnostics = false;
        public Random rand = new Random();
        int ClosestToCenter;
        public Byte[] DisplayBitmap;
        

        UInt32 getLongInteger(byte b0, byte b1, byte b2, byte b3)
        {
            convertToInteger.byte0 = b0;
            convertToInteger.byte1 = b1;
            convertToInteger.byte2 = b2;
            convertToInteger.byte3 = b3;
            return (convertToInteger.lngInteger);
        }

        UInt16 getShortInteger(byte b0, byte b1)
        {
            convertToInteger.byte0 = b0;
            convertToInteger.byte1 = b1;
            return (convertToInteger.shtInteger);
        }

        UInt16 BuildPacket(byte command, byte subcommand, ref byte[] ptrData, int dataLength, ref byte[] ptrPacket)
        {
            int i, j;
            byte[] arrCommandsAndData = new byte[MAXDATABYTES];

            j = 0;
            arrCommandsAndData[j++] = subcommand;  // Slave address comes first
            arrCommandsAndData[j++] = command;
            convertToInteger.shtInteger = (UInt16)dataLength;
            arrCommandsAndData[j++] = convertToInteger.byte0;
            arrCommandsAndData[j++] = convertToInteger.byte1;

            for (i = 0; i < dataLength; i++) arrCommandsAndData[j++] = ptrData[i];

            dataLength = dataLength + 4;
            ushort CRCvalue = MyCRC.CRCcalculate(ref arrCommandsAndData, dataLength, true);

            dataLength = dataLength + 2;

            int dataIndex = 0;
            if (dataLength <= MAXDATABYTES)
            {
                UInt16 packetIndex = 0;
                ptrPacket[packetIndex++] = STX;
                for (i = 0; i < dataLength; i++)
                {
                    byte dataByte = arrCommandsAndData[i];
                    if (dataByte == STX || dataByte == DLE || dataByte == ETX)
                        ptrPacket[packetIndex++] = DLE;
                    if (packetIndex >= MAXPACKETSIZE) return 0;
                    if (dataByte == ETX) dataByte = ETX - 1;
                    ptrPacket[packetIndex++] = dataByte;
                }
                ptrPacket[packetIndex++] = ETX;
                return (packetIndex);
            }
            else return (0);
        }

        public void InitializeSerialPorts()
        {
#if USE_SERIAL
            serialPort1.PortName = "COM3";
            serialPort1.BaudRate = 921600;
            serialPort1.Parity = 0;
            serialPort1.DataBits = 8;
            serialPort1.StopBits = System.IO.Ports.StopBits.One;
            serialPort1.Open();
#endif
        }

        private void CloseSerialPorts()
        {
#if USE_SERIAL
            try
            {
                //Dispose the In and Out buffers;
                serialPort1.DiscardInBuffer();
                serialPort1.DiscardOutBuffer();
                //Close the COM port
                serialPort1.Close();
            }
            //If there was an exeception then there isn't much we can
            //  do.  The port is no longer available.
            catch { }
#endif
        }


        public void TimeKeeper()
        {
            byte command, panelNumber, panelCommand;
            byte[] outData = new byte[LEDPanel.PANELSIZE + 16];
            byte[] arrPortInput = new byte[128];
            int BoardNumber = 0, BoardID = 0;
            int i, row, col;
            int packetLength = 0;
            tenthSeconds++;
            if (tenthSeconds > 10)
            {
                tenthSeconds = 0;
                seconds++;
                if (seconds > 60)
                {
                    seconds = 0;
                    minutes++;
                }
            }
            String strTime = String.Format("[{0}:{1}:{2}]", minutes, seconds, tenthSeconds);
            txtTimer.Text = strTime;

            if (OpMode == (int)Mode.RUN_TARGET) DisplayTarget();

            for (i = 0; i < LEDPanel.NUMPANELS; i++) MyPanels[i].CopyMatrixToPanel(ref XYmatrix);

#if USE_SERIAL
            byte[] outPacket = new byte[MAXPACKETSIZE];
                if (SerialPortsOpen)
                {
                    try
                    {
                        for (i = 0; i < LEDPanel.NUMPANELS; i++)
                        {
                        
                            BoardNumber = MyPanels[i].getBoardNumber();

                            // Shift Port ID to upper nibble
                            BoardID = MyPanels[i].getBoardNumber() * 16;

                            // panelNumber normally equals Port ID in upper nibble and panel ID in lower nibble
                            panelNumber = (byte) MyPanels[i].getPanelNumber();
                            panelCommand = (byte)(BoardID | (int) panelNumber);

                            packetLength = BuildPacket(0, panelCommand, ref MyPanels[i].arrPanelColorIndex, LEDPanel.PANELSIZE, ref outPacket);
                            serialPort1.Write(outPacket, 0, packetLength);
                        }
                    }
                    catch
                    {
                        try
                        {
                            serialPort1.DiscardInBuffer();
                            serialPort1.DiscardOutBuffer();
                            serialPort1.Close();
                            SerialPortsOpen = false;
                        }
                        catch { }
                    }
                }
#endif
        }

        public void DisplayTarget()
        {
           
            int i, inner, outer;

            FirstRadius++;
            if (FirstRadius >= Target[0].width)
            {
                FirstRadius = 1;
                int SumRadius = 0;
                int j = 0;
                // Find the last ring
                do
                {
                    if (Target[j].width == 0) break;
                    SumRadius = SumRadius + Target[j++].width;
                } while (SumRadius < MAXRADIUS || j < (MAXRADIUS - 1));
                // Move all rings out
                for (i = j; i > 0; i--)
                {
                    Target[i].width = Target[i - 1].width;
                    Target[i].colorIndex = Target[i - 1].colorIndex;
                }
                // Create new ring #0
                Target[0].width = rand.Next(1, 20);

                TargetColorIndex++;
                if (TargetColorIndex >= LEDPanel.MAXCOLOR) TargetColorIndex = 0;

                Target[0].colorIndex = TargetColorIndex;
            }

            inner = 0;
            outer = inner + Target[0].width;
            if (outer > FirstRadius) outer = FirstRadius;

            // Display all rings starting with center ring
            i = 0;
            do
            {
                colorIndex  = Target[i].colorIndex;
                if (outer >= MAXRADIUS - 1) outer = MAXRADIUS - 1;
                DrawPolarCircle(ref PolarMatrix, inner, outer, colorIndex);

                inner = outer;
                i++;
                if (i >= MAXRADIUS) i = 0;
                if (Target[i].width == 0) break;
                outer = inner + Target[i].width;
            } while (inner < MAXRADIUS - 2 && i < MAXRADIUS);

            PolarToMatrix(ref PolarMatrix, ref XYmatrix);
            ConvertMatrixToBitmap(ref XYmatrix, ref NewBitmapData);
            // Convert bitmap data array to stream
            MemoryStream displayStream = new MemoryStream(NewBitmapData);
            // Convert stream to bitmap
            bmpDisplay = new Bitmap(System.Drawing.Image.FromStream(displayStream));
            picDrawing.SizeMode = PictureBoxSizeMode.AutoSize;
            picDrawing.Location = new Point(0, 0);
            picDrawing.Image = bmpDisplay;

        }

        public void initializePanels()
        {            
            for (int i = 0; i < LEDPanel.NUMPANELS; i++)
            {
                MyPanels[i] = new LEDPanel();
                MyPanels[i].setOrientation(arrPanelInit[i, 0], arrPanelInit[i, 1], arrPanelInit[i, 2], arrPanelInit[i, 3], arrPanelInit[i, 4]);
            }

        }



        public MainForm()
        {
            int i, j, x, y, iXvalue, iYvalue, row, column;
            Byte redByte, greenByte, blueByte;
            double AngleRadians, AngleDegrees;
            InitializeComponent(); if (transDefaultFormMainForm == null) transDefaultFormMainForm = this;
                        
            BitmapHeader = new Byte[BITMAP_HEADER_SIZE];
            BitmapHeader[0x00] = (Byte)'B';
            BitmapHeader[0x01] = (Byte)'M';
            setLongBytes((UInt32)1920054, ref BitmapHeader[0x02], ref BitmapHeader[0x03], ref BitmapHeader[0x04], ref BitmapHeader[0x05]); // Size of file = 1920054
            BitmapHeader[0x06] = BitmapHeader[0x07] = BitmapHeader[0x08] = BitmapHeader[0x09] = (Byte)0;  // Unused
            setLongBytes((UInt32)54, ref BitmapHeader[0x0A], ref BitmapHeader[0x0B], ref BitmapHeader[0x0C], ref BitmapHeader[0x0D]); // Start of pixel data = 54   
            setLongBytes((UInt32)40, ref BitmapHeader[0x0E], ref BitmapHeader[0x0F], ref BitmapHeader[0x10], ref BitmapHeader[0x11]); // Number of bytes in DIB header from this point = 40
            setLongBytes((UInt32)800, ref BitmapHeader[0x12], ref BitmapHeader[0x13], ref BitmapHeader[0x14], ref BitmapHeader[0x15]); // Bitmap width in bytes = 800
            setLongBytes((UInt32)800, ref BitmapHeader[0x16], ref BitmapHeader[0x17], ref BitmapHeader[0x18], ref BitmapHeader[0x19]); // Bitmap height in bytes = 800
            setShortBytes((UInt16)1, ref BitmapHeader[0x1A], ref BitmapHeader[0x1B]);  // Number of color planes = 1
            setShortBytes((UInt16)24, ref BitmapHeader[0x1C], ref BitmapHeader[0x1D]);  // Number of bits per pixel = 24
            setLongBytes((UInt32)0, ref BitmapHeader[0x1E], ref BitmapHeader[0x1F], ref BitmapHeader[0x20], ref BitmapHeader[0x21]); // Compression = 0
            setLongBytes((UInt32)0, ref BitmapHeader[0x22], ref BitmapHeader[0x23], ref BitmapHeader[0x24], ref BitmapHeader[0x25]); // Size of bitmap data??? = 0
            setLongBytes((UInt32)11811, ref BitmapHeader[0x26], ref BitmapHeader[0x27], ref BitmapHeader[0x28], ref BitmapHeader[0x29]); // Resolution Horizontal??? = 11811
            setLongBytes((UInt32)11811, ref BitmapHeader[0x2A], ref BitmapHeader[0x2B], ref BitmapHeader[0x2C], ref BitmapHeader[0x2D]); // Resolution Vertical??? = 11811
            setLongBytes((UInt32)0, ref BitmapHeader[0x2E], ref BitmapHeader[0x2F], ref BitmapHeader[0x30], ref BitmapHeader[0x31]); // Colors in palette
            setLongBytes((UInt32)0, ref BitmapHeader[0x32], ref BitmapHeader[0x33], ref BitmapHeader[0x34], ref BitmapHeader[0x35]); // Important colors

            int testnum = 123;
            Console.WriteLine("Test Num =  {0:D}", testnum++);
            Console.WriteLine("Test Num =  {0:D}", testnum++);
            Console.WriteLine("Test Num =  {0:D}", testnum++);
            Console.Write("{0:D}, ", testnum++);


            this.Size = new System.Drawing.Size(1500, 1000);

            Load += new System.EventHandler(Form1_Load);
            timerRobotnik.Interval = 100;
            timerRobotnik.Tick += new System.EventHandler(timerRobotnik_Tick);

            picDrawing.SizeMode = PictureBoxSizeMode.AutoSize;
            picDrawing.Location = new Point(CX_OFFSET, CY_OFFSET);
            Byte[] imageData;

            btnOpenOrClosePort.Text = "OPEN PORTS";

            for (i = 0; i < LEDPanel.MAXCOLOR; i++) colorUsed[i] = false;

            Target = new Ring[MAXRADIUS];
            for (i = 0; i < MAXRADIUS; i++)
            {
                Target[i] = new Ring();
                Target[i].colorIndex = 15;
                Target[i].width = 0;
            }

            // Create ring #0
            Target[0].width = rand.Next(1, 10);
            Target[0].colorIndex = rand.Next(0, LEDPanel.MAXCOLOR);

            int width = Maribmp.Width;
            int height = Maribmp.Height;
            
            XYmatrix = new XYMatrixPoint[MATRIX_WIDTH, MATRIX_HEIGHT];
            // Initialize XY matrix
            for (i = 0; i < MATRIX_WIDTH; i++)
                for (j = 0; j < MATRIX_HEIGHT; j++)
                    XYmatrix[i,j] = new XYMatrixPoint();

            // Create polar matrix
            PolarMatrix = new PolarPointXY[MAX_ANGLE+1, MAXRADIUS];  // Angle in degrees, radius
            RotateMatrix = new PolarPointXY[MAX_ANGLE + 1, MAXRADIUS];  // Angle in degrees, radius
            for (i = 0; i <= MAX_ANGLE; i++)
                for (j = 0; j < MAXRADIUS; j++)
                {
                    PolarMatrix[i, j] = new PolarPointXY();
                    RotateMatrix[i, j] = new PolarPointXY();
                }

            // Initializepolar matrix
            InitializePolarMatrix(ref PolarMatrix, ref XYmatrix);
            InitializePolarMatrix(ref RotateMatrix, ref XYmatrix);

            initializePanels();
            for (int k = 0; k < LEDPanel.NUMPANELS; k++)
            {
                int BoardNumber = MyPanels[k].getBoardNumber();
                int BoardID = MyPanels[k].getBoardNumber() * 16;
            }

            // initMatrix();

            
            //MemoryStream ms = new MemoryStream();
            //Maribmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            //Byte[] bitmapData = new Byte[BITMAP_TOTAL_SIZE];
            //bitmapData = ms.ToArray();
            //ConvertBitmapToMatrix(ref bitmapData, ref XYmatrix);             
                       
            MatrixToPolar(ref XYmatrix, ref PolarMatrix);
            PolarToMatrix(ref PolarMatrix, ref XYmatrix);
            DisplayBitmap = new Byte[BITMAP_TOTAL_SIZE];           

            ConvertMatrixToBitmap(ref XYmatrix, ref DisplayBitmap);
            
            // Convert bitmap data array to stream
            MemoryStream displayStream = new MemoryStream(DisplayBitmap);
            // Convert stream to bitmap
            bmpDisplay = new Bitmap(System.Drawing.Image.FromStream(displayStream));
            picDrawing.SizeMode = PictureBoxSizeMode.AutoSize;
            picDrawing.Location = new Point(0, 0);
            picDrawing.Image = bmpDisplay;

            btnStartStop.Text = "START TARGET";
            // btnTest.Text = "START ROTATE";
        }
        
        int ColorBar(ref Color[,] ptrMatrix)
        {
            int row, column;
            int ColumnsPerColor = (int) (MATRIX_WIDTH / LEDPanel.MAXCOLOR);
            int ColorBarIndex = 0;
            int colorCounter = 0;
            System.Drawing.Color color;

            for (column = 0; column < MATRIX_WIDTH; column++)
            {
                if (ColorBarIndex >= LEDPanel.MAXCOLOR)
                    color = Color.White;
                else
                {
                    color = LEDPanel.colorWheel[ColorBarIndex];
                    colorCounter++;
                    if (colorCounter >= ColumnsPerColor)
                    {
                        colorCounter = 0;
                        ColorBarIndex++;
                    }
                }
                for (row = 0; row < MATRIX_HEIGHT; row++)
                    ptrMatrix[row, column] = color;
            }
            return 0;
        }
        

        void setLongBytes(UInt32 lngInteger, ref Byte b0, ref Byte b1, ref Byte b2, ref Byte b3)
        {
            convertToInteger.lngInteger = lngInteger;
            b0 = convertToInteger.byte0;
            b1 = convertToInteger.byte1;
            b2 = convertToInteger.byte2;
            b3 = convertToInteger.byte3;            
        }

        void setShortBytes(UInt16 shtInteger, ref Byte b0, ref Byte b1)
        {
            convertToInteger.shtInteger = shtInteger;
            b0 = convertToInteger.byte0;
            b1 = convertToInteger.byte1;
        }


        private static System.Timers.Timer timer;
        private const string ButtonTextOpenPort = "Open COM Port";
        private const string ButtonTextClosePort = "Close COM Port";
        private const string ModuleName = "COM Port Terminal";

        internal MainForm MyMainForm;
        internal PortSettingsDialog MyPortSettingsDialog;
        internal ComPorts UserPort1;

        private delegate void AccessFormMarshalDelegate(string action, string textToAdd, Color textColor);

        private Color colorReceive = Color.Green;
        private Color colorTransmit = Color.Red;
        private int maximumTextBoxLength;
        private string receiveBuffer;
        private bool savedOpenPortOnStartup;
        private int userInputIndex;
        public int minutes = 0, seconds = 0, tenthSeconds = 0;


        /// <summary>
        /// Look for COM ports and display them in the combo box.
        /// </summary>

        private void btnPort_Click(object sender, System.EventArgs e)   // $$$$
        {
            ComPorts.FindComPorts();
            MyPortSettingsDialog.DisplayComPorts();
            MyPortSettingsDialog.SelectComPort(UserPort1.SelectedPort.PortName);
            MyPortSettingsDialog.SelectBitRate(UserPort1.SelectedPort.BaudRate);
            MyPortSettingsDialog.SelectHandshaking(UserPort1.SelectedPort.Handshake);

            UserPort1.ParameterChanged = false;

            //  Display the combo boxes for setting port parameters.

            MyPortSettingsDialog.ShowDialog();
        }

        /// <summary>
        /// Create an instance of the ComPorts class.
        /// Initialize port settings and other parameters. 
        /// specify behavior on events.
        /// </summary>

        private void Form1_Load(object sender, System.EventArgs e)
        {
            Show();
            UserPort1 = new ComPorts();
            MyPortSettingsDialog = new PortSettingsDialog();
            tmrLookForPortChanges.Interval = 1000;
            tmrLookForPortChanges.Stop();

            //  Specify the routines that execute on events in other modules.
            //  The routines can receive data from other modules. 
            timerRobotnik.Stop();
        }

        /// <summary>
        /// Close the port if needed and save preferences.
        /// </summary>

        private void Form1_FormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {
            UserPort1.CloseComPort(UserPort1.SelectedPort);
        }


        // Default instance for Form

        private static MainForm transDefaultFormMainForm = null;
        public static MainForm TransDefaultFormMainForm
        {
            get
            {
                if (transDefaultFormMainForm == null)
                {
                    transDefaultFormMainForm = new MainForm();
                }
                return transDefaultFormMainForm;
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void btnPort_Click_1(object sender, EventArgs e)
        {

        }

        private void timerRobotnik_Tick(object sender, System.EventArgs e)
        {
            TimeKeeper();
        }


        void MatrixToPolar(ref XYMatrixPoint[,] XYmatrix, ref PolarPointXY[,] ptrPolar)
        {
            int i, iRadius;

            for (i = 0; i < MAX_ANGLE; i++)
            {
                for (iRadius = 0; iRadius < MAXRADIUS; iRadius++)
                {
                    int intX = ptrPolar[i, iRadius].X + CX_OFFSET;
                    int intY = ptrPolar[i, iRadius].Y + CY_OFFSET;
                    
                    if (intX < MATRIX_WIDTH && intY < MATRIX_HEIGHT)
                    {
                        int colorIndex = XYmatrix[intX, intY].colorIndex;
                        ptrPolar[i, iRadius].colorIndex = colorIndex;
                    }
                }
            }
        }


        void RotatePolar(ref PolarPointXY[,] ptrPolar)
        {
            int iAngle, iRadius;
            int colorIndex;
            int[] spare = new int[MAXRADIUS];

            
            // Stash last angle data
            for (iRadius = 0; iRadius < MAXRADIUS; iRadius++)
            {
                colorIndex = ptrPolar[0, iRadius].colorIndex;
                spare[iRadius] = colorIndex;
            }

            for (iAngle = 0; iAngle < MAX_ANGLE-1; iAngle++)
            {
                // Shift all data one angle clockwise
                for (iRadius = 0; iRadius < MAXRADIUS; iRadius++)
                {
                    colorIndex = ptrPolar[iAngle+1, iRadius].colorIndex;
                    ptrPolar[iAngle, iRadius].colorIndex = colorIndex;
                }
            }
            
            // Now copy last angle data to first angle
            for (iRadius = 0; iRadius < MAXRADIUS; iRadius++)
            {                
                colorIndex = spare[iRadius];
                ptrPolar[MAX_ANGLE-1, iRadius].colorIndex = colorIndex;
            }

        }


        int ConvertMatrixToBitmap(ref XYMatrixPoint[,] ptrMatrix, ref Byte[] ptrBitmap)
        {
            int i, row, column, m, n;
            int colorIndex;
            Color color;

            // Copy header to bitmap
            for (i = 0; i < BITMAP_DATA_OFFSET; i++)
                ptrBitmap[i] = BitmapHeader[i];

            // Add matrix data to bitmap
            for (row = 0; row < MATRIX_HEIGHT; row++)
            {
                for (m = 0; m < PIXELSIZE; m++)
                {
                    for (column = 0; column < MATRIX_WIDTH; column++)
                    {
                        colorIndex = ptrMatrix[column, row].colorIndex;
                        color = LEDPanel.colorWheel[colorIndex];
                        for (n = 0; n < PIXELSIZE; n++)
                        {
                            ptrBitmap[i++] = color.B;
                            ptrBitmap[i++] = color.G;
                            ptrBitmap[i++] = color.R;
                        }
                    }
                }
            }
            BitmapSize = i;
            return 0;
        }

        private void btnStartStop_Click(object sender, EventArgs e)
        {
            if (timerRobotnik.Enabled == false)
            {
                timerRobotnik.Start();
                OpMode = (int)Mode.RUN_TARGET;
                btnStartStop.Text = "STOP TARGET";                
            }
            else
            {
                timerRobotnik.Stop();
                btnStartStop.Text = "START TARGET";
            }
        }

        // MATRIX_WIDTH, MATRIX_HEIGHT
        // NUMBER_OF_ANGLES, MAXRADIUS
        void PolarToMatrix(ref PolarPointXY[,] ptrPolar, ref XYMatrixPoint[,] XYmatrix)
        {
            int i, iRadius;
            int colorIndex;

            for (i = 0; i < MAX_ANGLE; i++)
            {
                for (iRadius = 0; iRadius < MAXRADIUS; iRadius++)
                {
                    int intX = ptrPolar[i, iRadius].X + CX_OFFSET;
                    int intY = ptrPolar[i, iRadius].Y + CY_OFFSET;
                    colorIndex = ptrPolar[i, iRadius].colorIndex;
                    if (intX < MATRIX_WIDTH && intY < MATRIX_HEIGHT) XYmatrix[intX, intY].colorIndex = colorIndex; //  WHITE; 
                }
            }
            PreviousAngle = TestAngle;

            TestAngle++;
            if (TestAngle >= MAX_ANGLE) TestAngle = 0;
        }


        public void DisplayRotate()
        {
            RotatePolar(ref PolarMatrix);
            PolarToMatrix(ref PolarMatrix, ref XYmatrix);
            ConvertMatrixToBitmap(ref XYmatrix, ref NewBitmapData);
            // Convert bitmap data array to stream
            MemoryStream displayStream = new MemoryStream(NewBitmapData);
            // Convert stream to bitmap
            bmpDisplay = new Bitmap(System.Drawing.Image.FromStream(displayStream));
            picDrawing.SizeMode = PictureBoxSizeMode.AutoSize;
            picDrawing.Location = new Point(0, 0);
            picDrawing.Image = bmpDisplay;

        }


        private void btnOpenOrClosePort_Click_1(object sender, EventArgs e)
        {
#if USE_SERIAL
            if (!SerialPortsOpen)
            {
                InitializeSerialPorts();
                SerialPortsOpen = true;
                btnOpenOrClosePort.Text = "CLOSE PORTS";
            }
            else
            {
                CloseSerialPorts();
                SerialPortsOpen = false;
                btnOpenOrClosePort.Text = "OPEN PORTS";
            }
#endif
        }
        


        // MAX_ANGLE, MAXRADIUS
        void InitializePolarMatrix(ref PolarPointXY[,] ptrPolar, ref XYMatrixPoint[,] XYmatrix)
        {
            int iAngle, iRadius, intX = 0, intY = 0;
            double AngleRadians, CosValue, SinValue, dblY = 0, dblX = 0, dblAngleDegrees = 0;

            for (iAngle = 0; iAngle < MAX_ANGLE; iAngle++)
            {
                dblAngleDegrees = (double)iAngle * ANGLE_INCREMENT;
                AngleRadians = dblAngleDegrees * TORADIANS;

                // CosValue = CosTable[iAngle];
                CosValue = Math.Cos(AngleRadians);
                //SinValue = SinTable[iAngle];
                SinValue = Math.Sin(AngleRadians);

                for (iRadius = 0; iRadius < MAXRADIUS; iRadius++)
                {
                    double dblRadius = (double)iRadius;
                    dblX = dblRadius * CosValue;
                    intX = (int)Math.Round(dblX, 0.0);
                    dblY = dblRadius * SinValue;
                    intY = (int)Math.Round(dblY, 0.0);

                    ptrPolar[iAngle, iRadius].X = intX;
                    ptrPolar[iAngle, iRadius].Y = intY;
                    int X = intX + X_OFFSET;
                    int Y = intY + Y_OFFSET;
                    XYmatrix[X, Y].Angle = iAngle;
                    XYmatrix[X, Y].Radius = iRadius;
                    XYmatrix[X, Y].colorIndex = ptrPolar[iAngle, iRadius].colorIndex = LEDPanel.BLACK_INDEX;
                }
            }
        }

        private void btnBlackout_Click(object sender, EventArgs e)
        {
            byte[] outPacket = new byte[4];

            if (SerialPortsOpen)
            {
                try
                {
                    outPacket[0] = STX;
                    outPacket[1] = (byte) '0';
                    outPacket[2] = (byte) ' ';
                    outPacket[3] = ETX;
                    serialPort1.Write(outPacket, 0, 4);                    
                }
                catch
                {
                    try
                    {
                        serialPort1.DiscardInBuffer();
                        serialPort1.DiscardOutBuffer();
                        serialPort1.Close();
                        SerialPortsOpen = false;
                    }
                    catch { }
                }
            }

        }

        void SetColorPolar(ref PolarPointXY[,] ptrPolar, int colorIndex)
        {
            int i, iRadius;            

            for (i = 0; i < MAX_ANGLE; i++)
            {
                for (iRadius = 0; iRadius < MAXRADIUS; iRadius++)
                    ptrPolar[i, iRadius].colorIndex = colorIndex;
            }
        }



        private void btnTest_Click(object sender, EventArgs e)
        {
            int iAngleDegrees, iRadius, begin, end, i;
            double AngleRadians, CosValue, SinValue, dblY, dblX;
            byte command, panelNumber, panelCommand;
            byte[] outData = new byte[LEDPanel.PANELSIZE + 16];
            byte[] arrPortInput = new byte[128];
            int BoardNumber = 0, BoardID = 0;
            int row, col;
            int packetLength = 0;

            SetColorPolar(ref PolarMatrix, LEDPanel.BLACK_INDEX);

            RadiusTestIndex++;
            if (RadiusTestIndex >= MAXRADIUS) RadiusTestIndex = 0;
            DrawPolarCircle(ref PolarMatrix, RadiusTestIndex, RadiusTestIndex, LEDPanel.RED_INDEX);

            //ColorTestIndex++;  if (ColorTestIndex >= LEDPanel.MAXCOLOR) ColorTestIndex = 0;
            //PanelTestIndex++;  if(PanelTestIndex >= LEDPanel.NUMPANELS) PanelTestIndex = 0;
            //MyPanels[PanelTestIndex].SetPanelColor(ColorTestIndex);

            byte[] outPacket = new byte[MAXPACKETSIZE];
            if (SerialPortsOpen)
            {
                try
                {
                    for (i = 0; i < LEDPanel.NUMPANELS; i++)
                    {

                        BoardNumber = MyPanels[i].getBoardNumber();

                        // Shift Port ID to upper nibble
                        BoardID = MyPanels[i].getBoardNumber() * 16;

                        // panelNumber normally equals Port ID in upper nibble and panel ID in lower nibble
                        panelNumber = (byte)MyPanels[i].getPanelNumber();
                        panelCommand = (byte)(BoardID | (int)panelNumber);

                        packetLength = BuildPacket(0, panelCommand, ref MyPanels[i].arrPanelColorIndex, LEDPanel.PANELSIZE, ref outPacket);
                        serialPort1.Write(outPacket, 0, packetLength);
                    }
                }
                catch
                {
                    try
                    {
                        serialPort1.DiscardInBuffer();
                        serialPort1.DiscardOutBuffer();
                        serialPort1.Close();
                        SerialPortsOpen = false;
                    }
                    catch { }
                }
            }

            /*
            if (timerRobotnik.Enabled == false)
            {
                timerRobotnik.Start();
                OpMode = (int)Mode.RUN_ROTATE;
                btnTest.Text = "STOP ROTATE";
            }
            else
            {
                timerRobotnik.Stop();
                btnTest.Text = "START ROTATE";
            }
            */
        }

        void DrawPolarCircle(ref PolarPointXY[,] ptrPolar, int StartRadius, int StopRadius, int colorIndex)
        {
            int iAngleDegrees, iRadius, begin, end, i;
            double AngleRadians, CosValue, SinValue, dblY, dblX;
            byte command, panelNumber, panelCommand;
            byte[] outData = new byte[LEDPanel.PANELSIZE + 16];
            byte[] arrPortInput = new byte[128];
            int BoardNumber = 0, BoardID = 0;
            int row, col;
            int packetLength = 0;


            if (StartRadius >= MAXRADIUS) return;
            if (StopRadius >= MAXRADIUS) return;

            if (StartRadius <= StopRadius)
            {
                begin = StartRadius;
                end = StopRadius;
            }
            else
            {
                end = StartRadius;
                begin = StopRadius;
            }

            for (iRadius = begin; iRadius <= end; iRadius++)
            {
                for (i = 0; i < MAX_ANGLE; i++)
                    ptrPolar[i, iRadius].colorIndex = colorIndex;                    
            }

            /*
            PolarToMatrix(ref PolarMatrix, ref XYmatrix);
            ConvertMatrixToBitmap(ref XYmatrix, ref NewBitmapData);
            // Convert bitmap data array to stream
            MemoryStream displayStream = new MemoryStream(NewBitmapData);
            // Convert stream to bitmap
            bmpDisplay = new Bitmap(System.Drawing.Image.FromStream(displayStream));
            picDrawing.SizeMode = PictureBoxSizeMode.AutoSize;
            picDrawing.Location = new Point(0, 0);
            picDrawing.Image = bmpDisplay;

            // Console.WriteLine("Test Num =  {0:D}", testnum++);
            // Console.Write("{0:D}, ", testnum++);
            
            for (i = 0; i < LEDPanel.NUMPANELS; i++)
                MyPanels[i].CopyMatrixToPanel(ref XYmatrix);

            byte[] outPacket = new byte[MAXPACKETSIZE];
            if (SerialPortsOpen)
            {
                try
                {
                    for (i = 0; i < LEDPanel.NUMPANELS; i++)
                    {

                        BoardNumber = MyPanels[i].getBoardNumber();

                        // Shift Port ID to upper nibble
                        BoardID = MyPanels[i].getBoardNumber() * 16;

                        // panelNumber normally equals Port ID in upper nibble and panel ID in lower nibble
                        panelNumber = (byte)MyPanels[i].getPanelNumber();
                        panelCommand = (byte)(BoardID | (int)panelNumber);

                        packetLength = BuildPacket(0, panelCommand, ref MyPanels[i].arrPanelColorIndex, LEDPanel.PANELSIZE, ref outPacket);
                        serialPort1.Write(outPacket, 0, packetLength);
                    }
                }
                catch
                {
                    try
                    {
                        serialPort1.DiscardInBuffer();
                        serialPort1.DiscardOutBuffer();
                        serialPort1.Close();
                        SerialPortsOpen = false;
                    }
                    catch { }
                }
            }
            */
        }
    }   // End public partial class MainForm     
}  // namespace COMPortTerminal
