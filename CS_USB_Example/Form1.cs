using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UsbPrnControl
{
    public partial class Form1 : Form
    {
        int SendComing = 0;
        int txtOutState = 0;
        long oldTicks = DateTime.Now.Ticks, limitTick = 200;
        int LogLinesLimit = 100;
        public const byte Port1DataIn = 11;
        public const byte Port1DataOut = 12;
        public const byte Port1Error = 15;

        delegate void SetTextCallback1(string text);
        private void SetText(string text)
        {
            text = Accessory.FilterZeroChar(text);
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            //if (this.textBox_terminal1.InvokeRequired)
            if (textBox_terminal.InvokeRequired)
            {
                SetTextCallback1 d = new SetTextCallback1(SetText);
                BeginInvoke(d, new object[] { text });
            }
            else
            {
                int pos = textBox_terminal.SelectionStart;
                textBox_terminal.AppendText(text);
                if (textBox_terminal.Lines.Length > LogLinesLimit)
                {
                    StringBuilder tmp = new StringBuilder();
                    for (int i = textBox_terminal.Lines.Length - LogLinesLimit; i < textBox_terminal.Lines.Length; i++)
                    {
                        tmp.Append(textBox_terminal.Lines[i]);
                        tmp.Append("\r\n");
                    }
                    textBox_terminal.Text = tmp.ToString();
                }
                if (checkBox_autoscroll.Checked)
                {
                    textBox_terminal.SelectionStart = textBox_terminal.Text.Length;
                    textBox_terminal.ScrollToCaret();
                }
                else
                {
                    textBox_terminal.SelectionStart = pos;
                    textBox_terminal.ScrollToCaret();
                }
            }
        }

        private object threadLock = new object();
        public void collectBuffer(string tmpBuffer, int state)
        {
            if (tmpBuffer != "")
            {
                string time = DateTime.Today.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString("D3");
                lock (threadLock)
                {
                    if (!(txtOutState == state && (DateTime.Now.Ticks - oldTicks) < limitTick && state != Port1DataOut))
                    {
                        if (state == Port1DataIn) tmpBuffer = "<< " + tmpBuffer;         //sending data
                        else if (state == Port1DataOut) tmpBuffer = ">> " + tmpBuffer;    //receiving data
                        else if (state == Port1Error) tmpBuffer = "!! " + tmpBuffer;    //error occured

                        if (checkBox_saveTime.Checked == true) tmpBuffer = time + " " + tmpBuffer;
                        tmpBuffer = "\r\n" + tmpBuffer;
                        txtOutState = state;
                    }
                    if ((checkBox_saveInput.Checked == true && state == Port1DataIn) || (checkBox_saveOutput.Checked == true && state == Port1DataOut))
                    {
                        try
                        {
                            File.AppendAllText(textBox_saveTo.Text, tmpBuffer, Encoding.GetEncoding(RawPrnControl.Properties.Settings.Default.CodePage));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("\r\nError opening file " + textBox_saveTo.Text + ": " + ex.Message);
                        }
                    }
                    SetText(tmpBuffer);
                    oldTicks = DateTime.Now.Ticks;
                }
            }
        }

        public Form1()
        {
            InitializeComponent();
            RefreshPrnList();
        }

        private void button_Refresh_Click(object sender, EventArgs e)
        {
            RefreshPrnList();
        }

        private void button_Send_Click(object sender, EventArgs e)
        {
            if (comboBox_Printer.Items.Count > 0 && comboBox_Printer.SelectedItem.ToString() != "")
            {
                if (textBox_command.Text + textBox_param.Text != "")
                {
                    string outStr;
                    if (checkBox_hexCommand.Checked) outStr = textBox_command.Text;
                    else outStr = Accessory.ConvertStringToHex(textBox_command.Text);
                    if (checkBox_hexParam.Checked) outStr += textBox_param.Text;
                    else outStr += Accessory.ConvertStringToHex(textBox_param.Text);
                    if (outStr != "")
                    {
                        if (checkBox_hexTerminal.Checked) collectBuffer(outStr, Port1DataOut);
                        else collectBuffer(Accessory.ConvertHexToString(outStr), Port1DataOut);
                        textBox_command.AutoCompleteCustomSource.Add(textBox_command.Text);
                        textBox_param.AutoCompleteCustomSource.Add(textBox_param.Text);
                        RawPrinterSender.SendRAWToPrinter(comboBox_Printer.SelectedItem.ToString(), Accessory.ConvertHexToByteArray(outStr));
                    }
                }
            }
        }

        private void checkBox_hexCommand_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_hexCommand.Checked) textBox_command.Text = Accessory.ConvertStringToHex(textBox_command.Text);
            else textBox_command.Text = Accessory.ConvertHexToString(textBox_command.Text);
        }

        private void textBox_command_Leave(object sender, EventArgs e)
        {
            if (checkBox_hexCommand.Checked) textBox_command.Text = Accessory.CheckHexString(textBox_command.Text);
        }

        private void textBox_param_Leave(object sender, EventArgs e)
        {
            if (checkBox_hexParam.Checked) textBox_param.Text = Accessory.CheckHexString(textBox_param.Text);
        }

        private void checkBox_hexParam_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_hexParam.Checked) textBox_param.Text = Accessory.ConvertStringToHex(textBox_param.Text);
            else textBox_param.Text = Accessory.ConvertHexToString(textBox_param.Text);
        }

        private void button_Clear_Click(object sender, EventArgs e)
        {
            textBox_terminal.Clear();
        }

        private void checkBox_saveTo_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_saveInput.Checked || checkBox_saveOutput.Checked) textBox_saveTo.Enabled = false;
            else textBox_saveTo.Enabled = true;
        }

        private async void button_sendFile_ClickAsync(object sender, EventArgs e)
        {
            if (SendComing > 0)
            {
                SendComing++;
            }
            else if (SendComing == 0)
            {
                UInt16 repeat = 1, delay = 1, strDelay = 1;

                if (textBox_fileName.Text != "" && textBox_sendNum.Text != "" && UInt16.TryParse(textBox_sendNum.Text, out repeat) && UInt16.TryParse(textBox_delay.Text, out delay) && UInt16.TryParse(textBox_strDelay.Text, out strDelay))
                {
                    SendComing = 1;
                    button_Send.Enabled = false;
                    button_openFile.Enabled = false;
                    button_sendFile.Text = "Stop";
                    textBox_fileName.Enabled = false;
                    textBox_sendNum.Enabled = false;
                    textBox_delay.Enabled = false;
                    textBox_strDelay.Enabled = false;
                    for (int n = 0; n < repeat; n++)
                    {
                        string outStr = "";
                        string outErr = "";
                        long length = 0;
                        if (repeat > 1) collectBuffer(" Send cycle " + (n + 1).ToString() + "/" + repeat.ToString() + ">> ", 0);
                        try
                        {
                            length = new FileInfo(textBox_fileName.Text).Length;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("\r\nError opening file " + textBox_fileName.Text + ": " + ex.Message);
                        }
                        if (comboBox_Printer.Items.Count > 0 && comboBox_Printer.SelectedItem.ToString() != "")
                        {
                            //binary file read
                            if (!checkBox_hexFileOpen.Checked)
                            {
                                //byte-by-byte
                                if (radioButton_byByte.Checked)
                                {
                                    byte[] tmpBuffer = new byte[length];
                                    try
                                    {
                                        tmpBuffer = File.ReadAllBytes(textBox_fileName.Text);
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " + ex.Message);
                                    }
                                    for (int l = 0; l < tmpBuffer.Length; l++)
                                    {
                                        byte[] outByte = { tmpBuffer[l] };
                                        if (checkBox_hexTerminal.Checked) outStr = Accessory.ConvertByteArrayToHex(tmpBuffer, tmpBuffer.Length);
                                        else outStr = Encoding.GetEncoding(RawPrnControl.Properties.Settings.Default.CodePage).GetString(tmpBuffer);
                                        collectBuffer(outStr, Port1DataOut);

                                        if (RawPrinterSender.SendRAWToPrinter(comboBox_Printer.SelectedItem.ToString(), outByte))
                                        {
                                            progressBar1.Value = (n * tmpBuffer.Length + l) * 100 / (repeat * tmpBuffer.Length);
                                            if (strDelay > 0) await TaskEx.Delay(strDelay);
                                        }
                                        else
                                        {
                                            collectBuffer("Byte " + l.ToString() + ": Write Failure", Port1Error);
                                        }
                                        if (SendComing > 1) l = tmpBuffer.Length;
                                    }
                                }
                                //stream
                                else
                                {
                                    byte[] tmpBuffer = new byte[length];
                                    try
                                    {
                                        tmpBuffer = File.ReadAllBytes(textBox_fileName.Text);
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " + ex.Message);
                                    }
                                    int r = 0;
                                    while (r < 10 && !RawPrinterSender.SendRAWToPrinter(comboBox_Printer.SelectedItem.ToString(), tmpBuffer))
                                    {
                                        collectBuffer("USB write retry " + r.ToString(), Port1Error);
                                        await TaskEx.Delay(100);
                                        r++;
                                    }
                                    if (r >= 10) outErr = "Block write failure";
                                    if (checkBox_hexTerminal.Checked) outStr = Accessory.ConvertByteArrayToHex(tmpBuffer, tmpBuffer.Length);
                                    else outStr = Encoding.GetEncoding(RawPrnControl.Properties.Settings.Default.CodePage).GetString(tmpBuffer);
                                    if (outErr != "") collectBuffer(outErr + ": start", Port1Error);
                                    collectBuffer(outStr, Port1DataOut);
                                    if (outErr != "") collectBuffer(outErr + ": end", Port1Error);
                                    progressBar1.Value = ((n * tmpBuffer.Length) * 100) / (repeat * tmpBuffer.Length);
                                }
                            }
                            //hex file read
                            else
                            {
                                //String-by-string
                                if (radioButton_byString.Checked)
                                {
                                    String[] tmpBuffer = { };
                                    try
                                    {
                                        tmpBuffer = File.ReadAllText(textBox_fileName.Text).Replace('\n', '\r').Replace("\r\r", "\r").Split('\r');
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show("\r\nError reading file " + textBox_fileName.Text + ": " + ex.Message);
                                    }
                                    for (int l = 0; l < tmpBuffer.Length; l++)
                                    {
                                        if (tmpBuffer[l] != "")
                                        {
                                            tmpBuffer[l] = Accessory.CheckHexString(tmpBuffer[l]);
                                            collectBuffer(outStr, Port1DataOut);
                                            if (RawPrinterSender.SendRAWToPrinter(comboBox_Printer.SelectedItem.ToString(), Accessory.ConvertHexToByteArray(tmpBuffer[l])))
                                            {
                                                if (checkBox_hexTerminal.Checked) outStr = tmpBuffer[l];
                                                else outStr = Accessory.ConvertHexToString(tmpBuffer[l]);
                                                if (strDelay > 0) await TaskEx.Delay(strDelay);
                                            }
                                            else  //??????????????
                                            {
                                                outErr = "String" + l.ToString() + ": Write failure";
                                            }
                                            if (SendComing > 1) l = tmpBuffer.Length;
                                            collectBuffer(outErr, Port1Error);
                                            progressBar1.Value = (n * tmpBuffer.Length + l) * 100 / (repeat * tmpBuffer.Length);
                                        }
                                    }
                                }
                                //byte-by-byte
                                if (radioButton_byByte.Checked)
                                {
                                    string tmpStrBuffer = "";
                                    try
                                    {
                                        tmpStrBuffer = Accessory.CheckHexString(File.ReadAllText(textBox_fileName.Text));
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show("Error reading file " + textBox_fileName.Text + ": " + ex.Message);
                                    }
                                    byte[] tmpBuffer = new byte[tmpStrBuffer.Length / 3];
                                    tmpBuffer = Accessory.ConvertHexToByteArray(tmpStrBuffer);
                                    for (int l = 0; l < tmpBuffer.Length; l++)
                                    {
                                        byte[] outByte = { tmpBuffer[l] };
                                        if (checkBox_hexTerminal.Checked) outStr = Accessory.ConvertByteArrayToHex(tmpBuffer, tmpBuffer.Length);
                                        else outStr = Encoding.GetEncoding(RawPrnControl.Properties.Settings.Default.CodePage).GetString(tmpBuffer);
                                        collectBuffer(outStr, Port1DataOut);
                                        if (RawPrinterSender.SendRAWToPrinter(comboBox_Printer.SelectedItem.ToString(), outByte))
                                        {
                                            progressBar1.Value = (n * tmpBuffer.Length + l) * 100 / (repeat * tmpBuffer.Length);
                                            if (strDelay > 0) await TaskEx.Delay(strDelay);
                                        }
                                        else
                                        {
                                            collectBuffer("Byte " + l.ToString() + ": Write Failure", Port1Error);
                                        }
                                        if (SendComing > 1) l = tmpBuffer.Length;
                                    }
                                }
                                //stream
                                else
                                {
                                    string tmpStrBuffer = "";
                                    try
                                    {
                                        tmpStrBuffer = Accessory.CheckHexString(File.ReadAllText(textBox_fileName.Text));
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show("Error reading file " + textBox_fileName.Text + ": " + ex.Message);
                                    }
                                    byte[] tmpBuffer = new byte[tmpStrBuffer.Length / 3];
                                    tmpBuffer = Accessory.ConvertHexToByteArray(tmpStrBuffer);
                                    int r = 0;
                                    while (r < 10 && !RawPrinterSender.SendRAWToPrinter(comboBox_Printer.SelectedItem.ToString(), tmpBuffer))
                                    {
                                        collectBuffer("USB write retry " + r.ToString(), Port1Error);
                                        await TaskEx.Delay(100);
                                        r++;
                                    }
                                    if (r >= 10) outErr = "Block write failure";
                                    if (checkBox_hexTerminal.Checked) outStr = Accessory.ConvertByteArrayToHex(tmpBuffer, tmpBuffer.Length);
                                    else outStr = Encoding.GetEncoding(RawPrnControl.Properties.Settings.Default.CodePage).GetString(tmpBuffer);
                                    if (outErr != "") collectBuffer(outErr + " start", Port1Error);
                                    collectBuffer(outStr, Port1DataOut);
                                    if (outErr != "") collectBuffer(outErr + " end", Port1Error);
                                    progressBar1.Value = ((n * tmpBuffer.Length) * 100) / (repeat * tmpBuffer.Length);
                                }
                            }
                        }
                        if (repeat > 1) await TaskEx.Delay(delay);
                        if (SendComing > 1) n = repeat;
                    }
                    button_Send.Enabled = true;
                    button_openFile.Enabled = true;
                    button_sendFile.Text = "Send file";
                    textBox_fileName.Enabled = true;
                    textBox_sendNum.Enabled = true;
                    textBox_delay.Enabled = true;
                    textBox_strDelay.Enabled = true;
                }
                SendComing = 0;
            }
        }

        private void openFileDialog1_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            textBox_fileName.Text = openFileDialog1.FileName;
        }

        private void button_openFile_Click(object sender, EventArgs e)
        {
            if (checkBox_hexFileOpen.Checked == true)
            {
                openFileDialog1.FileName = "";
                openFileDialog1.Title = "Open file";
                openFileDialog1.DefaultExt = "txt";
                openFileDialog1.Filter = "HEX files|*.hex|Text files|*.txt|All files|*.*";
                openFileDialog1.ShowDialog();
            }
            else
            {
                openFileDialog1.FileName = "";
                openFileDialog1.Title = "Open file";
                openFileDialog1.DefaultExt = "bin";
                openFileDialog1.Filter = "BIN files|*.bin|PRN files|*.prn|All files|*.*";
                openFileDialog1.ShowDialog();
            }
        }

        private void checkBox_hexFileOpen_CheckedChanged(object sender, EventArgs e)
        {
            if (!checkBox_hexFileOpen.Checked)
            {
                radioButton_byString.Enabled = false;
                if (radioButton_byString.Checked) radioButton_byByte.Checked = true;
                checkBox_hexFileOpen.Text = "binary data";
            }
            else
            {
                radioButton_byString.Enabled = true;
                checkBox_hexFileOpen.Text = "hex text data";
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            RawPrnControl.Properties.Settings.Default.checkBox_hexCommand = checkBox_hexCommand.Checked;
            RawPrnControl.Properties.Settings.Default.textBox_command = textBox_command.Text;
            RawPrnControl.Properties.Settings.Default.checkBox_hexParam = checkBox_hexParam.Checked;
            RawPrnControl.Properties.Settings.Default.textBox_param = textBox_param.Text;
            RawPrnControl.Properties.Settings.Default.Save();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            checkBox_hexCommand.Checked = RawPrnControl.Properties.Settings.Default.checkBox_hexCommand;
            textBox_command.Text = RawPrnControl.Properties.Settings.Default.textBox_command;
            checkBox_hexParam.Checked = RawPrnControl.Properties.Settings.Default.checkBox_hexParam;
            textBox_param.Text = RawPrnControl.Properties.Settings.Default.textBox_param;
        }

        private void radioButton_stream_CheckedChanged(object sender, EventArgs e)
        {
            textBox_strDelay.Enabled = !radioButton_stream.Checked;
        }

        private void textBox_fileName_TextChanged(object sender, EventArgs e)
        {
            if (textBox_fileName.Text != "" && button_Send.Enabled == true) button_sendFile.Enabled = true;
            else button_sendFile.Enabled = false;
        }

        private void RefreshPrnList()
        {
            comboBox_Printer.Items.Clear();
            comboBox_Printer.Items.AddRange(RawPrinterSender.GetPrinterList());
            if (comboBox_Printer.Items.Count > 0)
            {
                comboBox_Printer.SelectedIndex = 0;
                if (comboBox_Printer.SelectedItem.ToString() != "")
                {
                    button_Send.Enabled = true;
                }
                else
                {
                    button_Send.Enabled = false;
                }
            }
        }

    }
}
