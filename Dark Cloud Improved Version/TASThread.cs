using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Dark_Cloud_Improved_Version
{
    class TASThread
    {
        public static int[] buttonInputs = new int[500000];
        public static int[] buttonInputs2 = new int[500000];
        public static int[] buttonInputs3 = new int[500000];
        public static int[] buttonInputs4 = new int[500000];
        public static int[] buttonInputs5 = new int[500000];
        public static int frameCounter;
        public static int frameCounterChecker;
        public static void RunTAS()
        {
            buttonInputs[108300] = (int)Button.Triangle;
            buttonInputs[108301] = (int)Button.Triangle;
            buttonInputs[108360] = (int)Button.Cross;
            buttonInputs[108361] = (int)Button.Cross;
            buttonInputs[108600] = (int)Button.Triangle;
            buttonInputs[108601] = (int)Button.Triangle;
            buttonInputs[108630] = (int)Button.DPad_Left;
            buttonInputs[108631] = (int)Button.DPad_Left;
            buttonInputs[108660] = (int)Button.Triangle;
            buttonInputs[108661] = (int)Button.Triangle;
            buttonInputs[108690] = (int)Button.DPad_Left;
            buttonInputs[108691] = (int)Button.DPad_Left;
            buttonInputs[108720] = (int)Button.Triangle;
            buttonInputs[108721] = (int)Button.Triangle;
            buttonInputs[108750] = (int)Button.DPad_Left;
            buttonInputs[108751] = (int)Button.DPad_Left;
            buttonInputs[108780] = (int)Button.Triangle;
            buttonInputs[108781] = (int)Button.Triangle;
            buttonInputs[108850] = (int)Button.Circle;
            buttonInputs[108851] = (int)Button.Circle;
            buttonInputs[108950] = (int)Button.DPad_Down;
            buttonInputs[108951] = (int)Button.DPad_Down;
            buttonInputs[109100] = (int)Button.Cross;
            buttonInputs[109101] = (int)Button.Cross;
            buttonInputs[109130] = (int)Button.DPad_Down;
            buttonInputs[109131] = (int)Button.DPad_Down;
            buttonInputs[109160] = (int)Button.DPad_Down;
            buttonInputs[109161] = (int)Button.DPad_Down;
            buttonInputs[109190] = (int)Button.DPad_Down;
            buttonInputs[109191] = (int)Button.DPad_Down;
            buttonInputs[109220] = (int)Button.DPad_Down;
            buttonInputs[109221] = (int)Button.DPad_Down;
            buttonInputs[109250] = (int)Button.Cross;
            buttonInputs[109251] = (int)Button.Cross;
            //buttonInputs[108500] = 64;
            //buttonInputs[108501] = 0;
            //buttonInputs[109000] = 64;
            //buttonInputs[109001] = 0;
            Memory.WriteUShort(0x300F7C6D, 37008);
            Memory.WriteUShort(0x300F7DE5, 37008);
            Memory.WriteUShort(0x300F7D87, 37008);
            Memory.WriteUShort(0x300F7D29, 37008);
            Memory.WriteUShort(0x300F7CCB, 37008);
            while (1 == 1)
            {
                frameCounter = Memory.ReadInt(0x202A2400);
                if (frameCounterChecker != frameCounter)
                {
                    if (buttonInputs[frameCounter] != 0)
                    {
                        Memory.WriteInt(0x21CBC544, buttonInputs[frameCounter]);
                    }
                    else
                    {
                        Memory.WriteInt(0x21CBC544, 0);
                    }
                    frameCounterChecker = frameCounter;
                }
               
            }
        }

        public static void RecordTAS()
        {
            while (frameCounter != 108500)
            {
                frameCounter = Memory.ReadInt(0x202A2400);

                if (frameCounterChecker != frameCounter)
                {
                    buttonInputs[frameCounter] = Memory.ReadInt(0x21CBC544);
                    buttonInputs2[frameCounter] = Memory.ReadInt(0x21CBC548);
                    buttonInputs3[frameCounter] = Memory.ReadInt(0x21CBC54C);
                    buttonInputs4[frameCounter] = Memory.ReadInt(0x21CBC550);
                    buttonInputs5[frameCounter] = Memory.ReadInt(0x21CBC554);

                    frameCounterChecker = frameCounter;
                }
            }
        }
    }
}
