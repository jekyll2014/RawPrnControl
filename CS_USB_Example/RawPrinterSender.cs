using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/*
private void button_SendString_Click()
{
    byte[] data = ConvertHexToByteArray(textBox_dataString.Text);
    byte[] inData = new byte[256];
    comboBox_printerList.AddRange(GetPrinterList());
    RawPrinterHelper.SendRAWToPrinter(comboBox_printerList.SelectedItem.ToString(), data);
    inData = RawPrinterHelper.ReadBytesFromPrinter(comboBox_printerList.SelectedItem.ToString(), data);
    textBox_printerAnswer.Text = ConvertByteArrToHex(inData, inData.Length);
}
*/

class RawPrinterSender
{
    // Structure and API declarions:
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
    }
    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern Int32 StartDocPrinter(IntPtr hPrinter, Int32 level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, Int32 dwCount, out Int32 dwWritten);

    [DllImport("winspool.Drv", EntryPoint = "ReadPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool ReadPrinter(IntPtr hPrinter, IntPtr pBuf, int cbBuf, out int pNoBytesRead);

    public static string[] GetPrinterList()
    {
        List<string> _printers = new List<string>();
        foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
        {
            _printers.Add(printer);
        }
        return _printers.ToArray();
    }

    // SendBytesToPrinter()
    // When the function is given a printer name and an unmanaged array
    // of bytes, the function sends those bytes to the print queue.
    // Returns true on success, false on failure.
    public static bool SendRAWToPrinter(string szPrinterName, byte[] data)
    {
        Int32 dwError = 0, dwWritten = 0;
        IntPtr hPrinter = new IntPtr(0);
        DOCINFOA di = new DOCINFOA();
        bool bSuccess = false; // Assume failure unless you specifically succeed.
        di.pDocName = "RAW PrintDocument";
        di.pDataType = "RAW";

        // Open the printer.
        if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
        {
            // Start a document.
            if (StartDocPrinter(hPrinter, 1, di) > 0)
            {
                // Start a page.
                if (StartPagePrinter(hPrinter))
                {
                    // Write your bytes.
                    //bSuccess = WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                    Int32 dwCount = data.Length;
                    IntPtr pStatusRequest = Marshal.AllocCoTaskMem(dwCount);
                    Marshal.Copy(data, 0, pStatusRequest, dwCount);
                    bSuccess = WritePrinter(hPrinter, pStatusRequest, dwCount, out dwWritten);

                    EndPagePrinter(hPrinter);
                    Marshal.FreeCoTaskMem(pStatusRequest);
                }
                EndDocPrinter(hPrinter);
            }
            ClosePrinter(hPrinter);
        }
        // If you did not succeed, GetLastError may give more information
        // about why not.
        if (bSuccess == false)
        {
            dwError = Marshal.GetLastWin32Error();
        }
        return bSuccess;
    }

    public static bool SendRAWToPrinter(string szPrinterName, string szString)
    {
        Byte[] bytes = Encoding.ASCII.GetBytes(szString);
        // Send the converted ANSI string to the printer.
        SendRAWToPrinter(szPrinterName, bytes);
        return true;
    }

    public static bool SendFileToPrinter(string szPrinterName, string szFileName)
    {
        // Open the file.
        FileStream fs = new FileStream(szFileName, FileMode.Open);
        // Create a BinaryReader on the file.
        BinaryReader br = new BinaryReader(fs);
        // Dim an array of bytes big enough to hold the file's contents.
        Byte[] bytes = new Byte[fs.Length];
        bool bSuccess = false;
        // Your unmanaged pointer.
        int nLength;

        nLength = Convert.ToInt32(fs.Length);
        // Read the contents of the file into the array.
        bytes = br.ReadBytes(nLength);
        // Send the unmanaged bytes to the printer.
        bSuccess = SendRAWToPrinter(szPrinterName, bytes);
        return bSuccess;
    }

    public static byte[] ReadBytesFromPrinter(string szPrinterName, byte[] statusRequest)
    {
        bool bSuccess = false;
        IntPtr hPrinter = new IntPtr(0);
        IntPtr hPrintJob = new IntPtr(0);
        byte[] status = new byte[0];
        // Open the printer.
        if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
        {
            // Start a document.
            DOCINFOA di = new DOCINFOA() { pDocName = "Read RAW PrintDocument", pDataType = "RAW" };
            Int32 jobId = StartDocPrinter(hPrinter, 1, di);
            if (jobId > 0)
            {
                // We can't read from a printer handle, but we can read from a printer job handle, 
                // So the trick is to create a Job using StartDocPrinter, then open a handle to the printer job...
                string jobName = string.Format("{0}, Job {1}", szPrinterName, jobId);
                if (OpenPrinter(jobName.Normalize(), out hPrintJob, IntPtr.Zero))
                {
                    // Start a page and print the Status-Request Sequence
                    if (StartPagePrinter(hPrinter))
                    {
                        {
                            Int32 dwCount = statusRequest.Length;
                            IntPtr pStatusRequest = Marshal.AllocCoTaskMem(dwCount);
                            Marshal.Copy(statusRequest, 0, pStatusRequest, dwCount);
                            Int32 dwWritten = 0;

                            bSuccess = WritePrinter(hPrinter, pStatusRequest, dwCount, out dwWritten);

                            EndPagePrinter(hPrinter);
                            EndDocPrinter(hPrinter);                        // EndPage and EndDoc here, otherwise ReadPrinter ist always null

                            Marshal.FreeCoTaskMem(pStatusRequest);
                        }
                        if (bSuccess)
                        {
                            //read request from "Job Handle"
                            Int32 bufLen = 32;
                            IntPtr pStatus = Marshal.AllocCoTaskMem(bufLen);
                            Int32 statusLen = 0;

                            bSuccess = ReadPrinter(hPrintJob, pStatus, bufLen, out statusLen);

                            int err = Marshal.GetLastWin32Error();          // Sometimes is error 0x3F : Your file waiting to be printed was deleted.

                            status = new byte[statusLen];
                            if (statusLen > 0) Marshal.Copy(pStatus, status, 0, statusLen);
                            Marshal.FreeCoTaskMem(pStatus);
                        }
                    }
                    ClosePrinter(hPrintJob);
                }
            }
            ClosePrinter(hPrinter);
        }
        return status;
    }
}
