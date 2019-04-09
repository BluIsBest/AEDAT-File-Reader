﻿using AEDAT_File_Reader.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace AEDAT_File_Reader
{
    public class CameraParameters
    {
        public readonly string cameraName;
        public readonly ushort cameraX;
        public readonly ushort cameraY;
        //int[] bitsXY;

        public CameraParameters(ushort cameraX, ushort cameraY, string cameraName)
        {
            this.cameraX = cameraX;
            this.cameraY = cameraY;
            this.cameraName = cameraName;
            //if(cameraName.name == "128")
            //{
            //    bitsXY = [2];
            //}
            //else
            //{
            //    bitsXY = [3];
            //}
        }

        public CameraParameters(ushort cameraX, ushort cameraY)
        {
            this.cameraX = cameraX;
            this.cameraY = cameraY;
        }

    }


    public static class AedatUtilities
    {
        //TODO: GET CAMERA TYPE & GETENDOFHEADERINDEX -> only grab the first 1MB or so


        public const int dataEntrySize = 8;     // How many bytes is in an AEDAT data entry

        // # HardwareInterface:
        public static readonly byte[] hardwareInterfaceCheck = new byte[20] { 0x23, 0x20, 0x48, 0x61, 0x72, 0x64, 0x77, 0x61, 0x72, 0x65, 0x49, 0x6e, 0x74, 0x65, 0x72, 0x66, 0x61, 0x63, 0x65, 0x3a };


        /// <summary>
        /// Iterates through an AEDAT file to find the end of the header.
        /// </summary>
        /// <param name="fileBytes"></param>
        /// <returns>Returns an int which signifies the position in the file where the header ends.</returns>
        public static int GetEndOfHeaderIndex(ref byte[] fileBytes)
        {
            bool foundEndOfHeader = false;
            const int headerCheckSize = 23;         // Number of elements in the header check
            byte[] currentHeaderBytes = new byte[headerCheckSize];

            //Compare current bytes being red to find end of header. (#End Of ASCII)
            byte[] endOfHeaderCheck = new byte[headerCheckSize] { 0x0a, 0x23, 0x45, 0x6e, 0x64, 0x20, 0x4f, 0x66, 0x20, 0x41, 0x53, 0x43, 0x49, 0x49, 0x20, 0x48, 0x65, 0x61, 0x64, 0x65, 0x72, 0x0d, 0x0a };

            Queue<byte> headerCheckQ = new Queue<byte>();

            int endOfHeaderIndex = 0;
            foreach (byte byteIn in fileBytes)
            {
                if (!foundEndOfHeader)
                {
                    headerCheckQ.Enqueue(byteIn);

                    // Remove oldest element in the queue if it becomes too large. FIFO
                    if (headerCheckQ.Count > headerCheckSize) headerCheckQ.Dequeue();

                    headerCheckQ.CopyTo(currentHeaderBytes, 0);
                    if (Enumerable.SequenceEqual(endOfHeaderCheck, currentHeaderBytes))
                    {
                        foundEndOfHeader = true;
                    }
                    endOfHeaderIndex++;
                }
                else
                {
                    break;
                }
            }

            return endOfHeaderIndex;
        }


        /// <summary>
        /// Searches for a particular line in the AEDAT header
        /// </summary>
        /// <param name="search"></param>
        /// <param name="fileBytes"></param>
        /// <returns>The line searched for, if found</returns>
        public static string FindLineInHeader(byte[] search, ref byte[] fileBytes)
        {
            bool foundSearch = false;
            int checkLength = search.Length;
            byte[] currentCheckBytes = new byte[checkLength];

            Queue<byte> searchCheckQ = new Queue<byte>();

            int endOfCheckIndex = 0;
            int startOfLineIndex = -1;
            int endOfLineIndex = -1;
            byte[] newLine = Encoding.ASCII.GetBytes(Environment.NewLine);

            foreach (byte byteIn in fileBytes)
            {
                if (!foundSearch)
                {
                    searchCheckQ.Enqueue(byteIn);

                    // Remove oldest element in the queue if it becomes too large. FIFO
                    if (searchCheckQ.Count > checkLength) searchCheckQ.Dequeue();

                    searchCheckQ.CopyTo(currentCheckBytes, 0);
                    if (Enumerable.SequenceEqual(hardwareInterfaceCheck, currentCheckBytes))
                    {
                        foundSearch = true;
                        startOfLineIndex = endOfCheckIndex - searchCheckQ.Count;
                        endOfLineIndex = startOfLineIndex;
                        break;
                    }
                    endOfCheckIndex++;
                }
            }

            // Search for newline character
            int newlineAttempts = 0;
            while (true)
            {
                if (fileBytes[endOfLineIndex] != newLine[0] && fileBytes[endOfLineIndex + 1] != newLine[1]) // hh
                {
                    endOfLineIndex++;
                    newlineAttempts++;
                }
                else
                {
                    break;
                }

                if (newlineAttempts > 10000)
                {
                    return ("ERROR: Could not find newLine character");
                }

            }

            var searchReturn = fileBytes.Skip(startOfLineIndex).Take(newlineAttempts);
            return Encoding.UTF8.GetString(searchReturn.ToArray());
        }

        public static CameraParameters ParseCameraModel(string s)
        {
            switch (s)
            {
                case string dvs128 when dvs128.Contains("DVS128"): return new CameraParameters(128, 128, "DVS128");
                case string dvs240 when dvs240.Contains("DAVIS240"): return new CameraParameters(240, 180, "DAVIS240");
                default: return null;
            }


        }


        /// <summary>
        /// Extracts the event type from a data entry byte array.
        /// </summary>
        /// <param name="dataEntry"></param>
        /// <returns>Returns true for an ON event, false for an OFF event.</returns>
        public static bool GetEventType(byte[] dataEntry)
        {
            return ((dataEntry[5] >> 3) & 1) == 1;     //Event type is located in the fourth bit of the sixth byte
        }


        /// <summary>
        /// Gets the XY coordinates from the provided data entry.
        /// </summary>
        /// <param name="dataEntry"></param>
        /// <returns>Returns a uint16 array containing the XY coordinates.</returns>
        public static int[] GetXYCords240(byte[] dataEntry, int height, int width)
        {
            int[] xy = new int[2];
            BitArray bits = new BitArray(dataEntry);

            // X
            bool[] cord = new bool[] { bits[44], bits[45], bits[46], bits[47], bits[48], bits[49], bits[50], bits[51], bits[52], bits[53] };
            xy[0] = width - BoolArrayToInt(cord);

            // Y
            cord = new bool[] { bits[54], bits[55], bits[56], bits[57], bits[58], bits[59], bits[60], bits[61], bits[62] };
            xy[1] = height - BoolArrayToInt(cord);

            return xy;
        }

    

        public static int[] GetXYCords128(byte[] dataEntry, int height, int width )
        {
            int[] xy = new int[2];
            BitArray bits = new BitArray(dataEntry);

            // X
            bool[] cord = new bool[] { bits[33], bits[34], bits[35], bits[36], bits[37], bits[38], bits[39]};
            xy[0] = width - BoolArrayToInt(cord);

            // Y
            cord = new bool[] { bits[40], bits[41], bits[42], bits[43], bits[44], bits[45], bits[46]};
            xy[1] = height - BoolArrayToInt(cord);

            return xy;
        }

        private static int BoolArrayToInt(bool[] bools)
        {
            int word = 0;

            for (int i = 0; i < bools.Length; i++)
            {
                if (bools[i])
                {
                    int twoToPower = (1 << i);
                    word = (word + twoToPower);
                }
            }
            return word;
        }

        public static async Task<List<Event>> GetEvents(StorageFile file)
        {

            byte[] result = await readToBytes(file);      // All of the bytes in the AEDAT file loaded into an array

            List<Event> tableData = new List<Event>();
            const int dataEntrySize = 8;            // Number of elements in the data entry

            byte[] currentDataEntry = new byte[dataEntrySize];

            string cameraTypeSearch = AedatUtilities.FindLineInHeader(AedatUtilities.hardwareInterfaceCheck, ref result);
            CameraParameters cam = AedatUtilities.ParseCameraModel(cameraTypeSearch);
            if (cam is null)
            {
                ContentDialog invalidData = new ContentDialog()
                {
                    Title = "Error",
                    Content = "Could not parse camera parameters.",
                    CloseButtonText = "Close"
                };

                return null;
            }

            int endOfHeaderIndex = AedatUtilities.GetEndOfHeaderIndex(ref result);

            int timeStamp = 0;

            Func<byte[], int, int, int[]> getXY = AedatUtilities.GetXY_Cam(cam.cameraName);


            for (int i = endOfHeaderIndex; i < result.Count() - 1; i += 8)
            {
                for (int j = 7; j > -1; j--)
                {
                    currentDataEntry[j] = result[i + j];

                }
                Array.Reverse(currentDataEntry);
                timeStamp = BitConverter.ToInt32(currentDataEntry, 0);      // Timestamp is found in the first four bytes

                int[] XY = getXY(currentDataEntry, cam.cameraY, cam.cameraX);
                tableData.Add(new Event { time = timeStamp, onOff = AedatUtilities.GetEventType(currentDataEntry), x = XY[0], y = XY[1] });


            }




            return tableData;


        }

        public static async Task<byte[]> readToBytes(StorageFile file)
        {
            byte[] result;
            using (Stream stream = await file.OpenStreamForReadAsync())
            {
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    result = memoryStream.ToArray();
                }
            }

            return result;
        }

        public static Func<byte[], int, int, int[]> GetXY_Cam(string cameraName)
        {
            Func<byte[], int, int, int[]> getXY;
            switch (cameraName)
            {
                case "DVS128":
                    getXY = AedatUtilities.GetXYCords128;
                    break;
                case "DAVIS240":
                    getXY = AedatUtilities.GetXYCords240;
                    break;
                default:
                    getXY = AedatUtilities.GetXYCords240;
                    break;
            }
            return getXY;
        }

        public static void setPixel(ref byte[] pixels, int x, int y, byte[] rgba, int imageWidth)
        {

            y = y - 1;
            x = x - 1;

            int startingPoint = (((imageWidth * y) + x) * 4);

            pixels[startingPoint] = rgba[2];
            pixels[startingPoint + 1] = rgba[1];
            pixels[startingPoint + 2] = rgba[0];
            pixels[startingPoint + 3] = rgba[3];
        }
    }
}
