using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
//using CameraCapture;
using COMPortTerminal;
using System.Drawing;

namespace PanelSpace
{
    public class LEDPanel
    {
        public const int NUMPANELS = 24;  
        public const int    PANELROWS = 16;
        public const int    PANELCOLS = 32;
        public const int    NUMCHANNELS = 3; //  CameraCapture.CameraCapture.NUMCHANNELS;
        public const int    PANELSIZE = PANELROWS * PANELCOLS; //  * NUMCHANNELS;        $$$$
        public const int    HORIZONTAL = 0;
        public const int    VERTICAL = 1;
        public const int    HORIZONTAL_FLIPPED = 2;
        public const int    VERTICAL_FLIPPED = 3;
        public int          boardNumber= 1;
        public int          orientation;
        public int          ZeroCol, ZeroRow;
        public byte         subCommand;
        public Byte[]       arrPanelColorIndex = new Byte[PANELSIZE];
        public const int    MAXCOLOR = 16;

        public const int MAGENTA_INDEX = 0;
        public const int PURPLE_INDEX = 1;
        public const int CYAN_INDEX = 2;
        public const int LIME_INDEX = 3;
        public const int YELLOW_INDEX = 4;
        public const int ORANGE_INDEX = 5;
        public const int RED_INDEX = 6;
        public const int GREEN_INDEX = 7;
        public const int BLUE_INDEX = 8;
        public const int PINK_INDEX = 9;
        public const int LAVENDER_INDEX = 10;
        public const int TURQUOISE_INDEX = 11;
        public const int WHITE_INDEX = 12;
        public const int GRAY_INDEX = 13;
        public const int DARK_GRAY_INDEX = 14;
        public const int BLACK_INDEX = 15;

        public static readonly System.Drawing.Color[] colorWheel = { Color.Magenta, Color.Purple, Color.Cyan, Color.Lime, Color.Yellow, Color.Orange, Color.Red, Color.Green, Color.Blue, Color.Pink, Color.Lavender, Color.Turquoise, Color.White, Color.Gray, Color.DarkGray, Color.Black};

        public LEDPanel()
        {            
            ZeroCol = 0;
            ZeroRow = 0;
            orientation = HORIZONTAL;
            boardNumber= 1;

            for (int i = 0; i < PANELSIZE; i++)
                arrPanelColorIndex[i] = BLACK_INDEX;
        }

        public void setOrientation(int boardNumber, byte subCommand, int orientation, int ZeroRow, int ZeroCol)
        {
            this.ZeroRow = ZeroRow;
            this.ZeroCol = ZeroCol;
            this.orientation = orientation;
            this.subCommand = subCommand;
            this.boardNumber= boardNumber;
        }

        public Byte getPanelColor(int index)
        {
            return (arrPanelColorIndex[index]);
        }

        public int getBoardNumber()
        {
            return (boardNumber);
        }

        public byte getPanelNumber()
        {
            return (subCommand);
        }


        byte ConvertColorToByte(Color color)
        {
            UInt16 Red, Green, Blue;

            //Red = (UInt16) (color.R & 0xE0);
            //Green = (UInt16)(color.G & 0xE0);
            //Blue = (UInt16)(color.G & 0xC0);

            Red = (UInt16) color.R;
            Green = (UInt16)color.G;
            Blue = (UInt16)color.G;

            if ((Red > Green) && (Blue > Green)) Green = 0;
            else if ((Red > Blue) && (Green > Blue)) Blue = 0;
            else if ((Green > Red) && (Blue > Red)) Red = 0;
            else Green = 0;

            UInt16 colorInt;
            colorInt = (UInt16)((Red & 0xE0) | ((Green & 0xE0) >> 3) | (Blue >> 6));
            if (colorInt > 255) colorInt = 255;
            return (Byte)colorInt;
        }

        public void CopyMatrixToPanel(ref XYMatrixPoint[,] matrix)
        {
            int row, col, i;
            i = 0;
            if (orientation == HORIZONTAL)
            {
                for (row = ZeroRow; row < ZeroRow + PANELROWS; row++)
                    for (col = ZeroCol; col < ZeroCol + PANELCOLS; col++)
                        arrPanelColorIndex[i++] = (byte) matrix[col, row].colorIndex;
            }
            else if (orientation == VERTICAL)
            {
                for (col = ZeroCol + PANELROWS - 1; col >= ZeroCol; col--)
                    for (row = ZeroRow; row < row + PANELCOLS; row++)
                        arrPanelColorIndex[i++] = (byte)matrix[col, row].colorIndex;
            }
            else if (orientation == HORIZONTAL_FLIPPED)
            {
                for (row = ZeroRow + PANELROWS - 1; row >= ZeroRow; row--)
                    for (col = ZeroCol + PANELCOLS + 1; col >= ZeroCol; col--)
                        arrPanelColorIndex[i++] = (byte)matrix[col, row].colorIndex;
            }
            else
            {
                for (col = ZeroCol; col < ZeroCol + PANELROWS; col++)
                    for (row = ZeroRow + PANELCOLS - 1; row >= ZeroRow; row--)
                        arrPanelColorIndex[i++] = (byte)matrix[col, row].colorIndex;
            }
        }

        public void SetPanelColor(byte ColorIndex)
        {
            int row, col, i;
            i = 0;
            if (orientation == HORIZONTAL)
            {
                for (row = ZeroRow; row < ZeroRow + PANELROWS; row++)
                    for (col = ZeroCol; col < ZeroCol + PANELCOLS; col++)
                        arrPanelColorIndex[i++] = ColorIndex;
            }
            else if (orientation == VERTICAL)
            {
                for (col = ZeroCol + PANELROWS - 1; col >= ZeroCol; col--)
                    for (row = ZeroRow; row < row + PANELCOLS; row++)
                        arrPanelColorIndex[i++] = ColorIndex;
            }
            else if (orientation == HORIZONTAL_FLIPPED)
            {
                for (row = ZeroRow + PANELROWS - 1; row >= ZeroRow; row--)
                    for (col = ZeroCol + PANELCOLS + 1; col >= ZeroCol; col--)
                        arrPanelColorIndex[i++] = ColorIndex;
            }
            else
            {
                for (col = ZeroCol; col < ZeroCol + PANELROWS; col++)
                    for (row = ZeroRow + PANELCOLS - 1; row >= ZeroRow; row--)
                        arrPanelColorIndex[i++] = ColorIndex;
            }
        }

    }
}



