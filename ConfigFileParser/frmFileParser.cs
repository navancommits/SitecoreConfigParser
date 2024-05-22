using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static ConfigFileParser.frmFileParser;

namespace ConfigFileParser
{
    public partial class frmFileParser : Form
    {
        private string parsePath = "C:\\backup\\config";
        private string[] lstConfig;
        private LineRange lineRange;
        private string configFileData;
        private List<PipelineInfo> pipelineInfoList;
        private List<ProcessorInfo> processorInfoList;
        private string keywordBeginTag;
        private string keywordClosingTag;
        private int intLineNumTracker;
        private string previousClosingTag = "dummy";
        private bool tagOpened;
        private string flag;
        bool processorTagCommented = false;
        private int runningPipelineIndex = 0;
        private int processorSerialNumber = 0;
        private int pipelineSerialNumber = 0;

        public frmFileParser()
        {
            InitializeComponent();
            //flag = "pipeline";
            flag = "all";
        }

        private string ExtractString(string originalString, string firstString, string nextString)
        {
            string FinalString;
            int Pos1 = originalString.Trim().IndexOf(firstString) + firstString.Length;
            int Pos2 = originalString.Trim().IndexOf(nextString);
            FinalString = originalString.Trim().Substring(Pos1, Pos2 - Pos1);

            return FinalString;
        }

        internal class LineRange
        {
            internal int StartLineIndex { get; set; }
            internal int EndLineIndex { get; set; }

        }

        internal class PipelineInfo
        {
            internal string Comment { get; set; }
            internal string Name { get; set; }
            internal string FileName { get; set; }
            internal int SerialNumber { get; set; }
            internal List<ProcessorInfo> ProcessorInfoList { get; set; }
        }

        internal class ProcessorInfo
        {
            internal string Type { get; set; }
            internal string Method { get; set; }
            internal string Name { get; set; }
            internal string Comment { get; set; }
            internal int SerialNumber { get; set; }
        }

        private string[] ExtractArraywithSplit(string content, string keyword)
        {
            return content.Split(new string[] { keyword }, StringSplitOptions.None);
        }

        private string GetComment(string line, int linetracker)
        {
            string comment = string.Empty;

            if (line.Trim().EndsWith("-->"))
            {
                if (line.Trim().StartsWith("<!--"))
                {
                    comment = ExtractString(line.Trim(), "<!--", "-->");
                }
                else
                {
                    //since cursor ran past, have to go back and concat the comments 
                    comment = StraightenLinefromRight(linetracker);
                }
            }

            return comment;
        }

        private void ParseFile(string filePath, string parentkeyword)
        {
            configFileData = File.ReadAllText(filePath);
            PipelineInfo pipelineInfo = new PipelineInfo();

            if (!(configFileData.ToLowerInvariant().Contains(parentkeyword)))
                return;//no need to parse

            string strConfigText = configFileData;
            lstConfig = strConfigText.Split(new Char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            processorInfoList = new List<ProcessorInfo>();
            for (intLineNumTracker = lineRange.StartLineIndex; intLineNumTracker <= lineRange.EndLineIndex; intLineNumTracker++)
            {
                if (IsBlankLine(intLineNumTracker)) continue;
                //if (IsCommentedLine() && !processorTagCommented) continue;

                var currentLine = lstConfig[intLineNumTracker];
                int intOrigIndex = intLineNumTracker;
                if (!currentLine.Trim().EndsWith(">")) currentLine = StraightenLine(intLineNumTracker);
                //if (currentLine.Trim().StartsWith("<!--") && currentLine.Trim().Contains("<processor ")) processorTagCommented = true;
                processorTagCommented = ProcessorLineCommented(currentLine);

                if (currentLine.Trim().Contains("<processor ") && !IsCommentedLine())
                {
                    //first processor tag enters here and gets the pipeline info too
                    //retrieve prev line
                    int tmpprevlinenum = lstConfig[intOrigIndex - 1].Contains("-->") ? GetValidLineNumber(intOrigIndex - 1) : intOrigIndex - 1;
                    var tmpprevline = lstConfig[tmpprevlinenum];
                    string previousLine;

                    if (string.IsNullOrWhiteSpace(tmpprevline.Trim())) tmpprevline = lstConfig[tmpprevlinenum - 1];
                    if (!(tmpprevline.Contains("<") && (tmpprevline.Contains(">")))) { previousLine = StraightenProcessorLinefromRight(intOrigIndex - 1); } 
                    else { previousLine = tmpprevline; }

                    var tmpcommentline = lstConfig[tmpprevlinenum - 1];
                    string pipelinecommentline;

                    //if (string.IsNullOrWhiteSpace(tmpcommentline.Trim())) tmpcommentline = lstConfig[tmpprevlinenum - 2];
                    if (tmpcommentline.Contains("-->") && (!tmpcommentline.Contains("<!--"))) 
                        { pipelinecommentline = StraightenLinefromRight(intOrigIndex - 2); }
                    else { pipelinecommentline = tmpcommentline; }

                    //now check if prev line isn't anything else 
                    if (!previousLine.Trim().StartsWith("<pipelines") && !previousLine.Trim().StartsWith("<processor") && !previousLine.Trim().StartsWith("</processor") && !string.IsNullOrWhiteSpace(previousClosingTag))
                    {
                        string comment = GetComment(pipelinecommentline, intOrigIndex - 2);

                        if (currentLine.Trim().Contains(" help="))
                            comment += " " + ExtractArraywithSplit(currentLine.Trim(), " help=")[1];

                        pipelineInfo = new PipelineInfo
                        {
                            Name = ExtractString(previousLine, "<", ">").Split(' ')[0],//actual extraction
                            Comment = comment.Replace("<!--", string.Empty).Replace("-->", string.Empty)
                        };

                        var pathSplit = ExtractArraywithSplit(filePath, "\\");
                        pipelineInfo.FileName = pathSplit[pathSplit.Length - 1];

                        if (!processorTagCommented) ParseProcessorLinesbetweenProcessorTags(currentLine);
                    }
                    else
                    {
                        //additional processor tags within an existing pipeline
                        if (!processorTagCommented) ParseProcessorLinesbetweenProcessorTags(currentLine);
                    }

                    tagOpened = true;
                    
                }
                else
                {
                    //rest of lines enter this flow
                    AddtoListandReset(currentLine, pipelineInfo);
                }

                    processorTagCommented = false;
             }
                lineRange = new LineRange();//re-initialize
                AddtoListandReset(lstConfig[intLineNumTracker - 1], pipelineInfo);
        }

        private void AddtoListandReset(string currentLine, PipelineInfo pipelineInfo)
        {
            if (tagOpened)
            {
                var closingTag = $"</{pipelineInfo.Name}>";
                if (currentLine.Trim().StartsWith(closingTag))
                {
                    pipelineInfo.ProcessorInfoList = processorInfoList;
                    pipelineInfoList.Add(pipelineInfo);
                    processorInfoList = new List<ProcessorInfo>();
                    //lineRange.StartLineIndex = 0;
                    //lineRange.EndLineIndex = 0;
                    previousClosingTag = closingTag;
                    tagOpened = false;
                }
            }
        }

        private bool IsBlankLine(int currLine)
        {
            if (string.IsNullOrWhiteSpace(lstConfig[currLine])) return true;

            return false;
        }

        private bool IsCommentedLine()
        {
            var tmpcommentline = lstConfig[intLineNumTracker];
            string singleCommentedline;

            if (!((tmpcommentline.Contains("<!--") || tmpcommentline.Contains("-->")))) return false;

            if (tmpcommentline.Contains("<!--") && (!tmpcommentline.Contains("-->"))) { singleCommentedline = StraightenLinefromLeft(); }
            else { singleCommentedline = tmpcommentline; }

            if (singleCommentedline.Trim().StartsWith("<!--") && singleCommentedline.Trim().EndsWith("-->"))
            {
                if (singleCommentedline.Contains("<processor ")) {
                    processorTagCommented = true;
                    return true;
                }
            }

            return false;
        }

        private List<ProcessorInfo> AddProcessorInfofromLine(ProcessorInfo processorInfo, string actualLine, string previousline = "")
        {
            string processorLine = actualLine;
            //extract processor details

            string[] processorLineSplit = processorLine.Split(',');
            string type = ExtractArraywithSplit(processorLine, "type=")[1];
            string methodName = string.Empty;

            if (type.Contains("method=")) methodName = ExtractArraywithSplit(type, "method=")[1].Split(' ')[0];

            string[] splitType = processorLineSplit[0].Split('.');
            processorSerialNumber += 1;
            processorInfo.Name = splitType[splitType.Length - 1];

            if (type.Contains(" method=")) type = ExtractArraywithSplit(type, " method=")[0];
            if (type.Contains(" resolve="))
                    type = ExtractArraywithSplit(type, " resolve=")[0];
            if (type.Contains(" patch:")) type = ExtractArraywithSplit(type, " patch:")[0];
            type = type.Replace(">",string.Empty);
            type = type.Replace("/", string.Empty);
            type = type.Replace("]", string.Empty);

            processorInfo.Type = type;
            processorInfo.Method = methodName;
            processorInfo.SerialNumber = processorSerialNumber;

            if (!string.IsNullOrWhiteSpace(previousline)) processorInfo.Comment = GetComment(previousline, intLineNumTracker - 1);

            processorInfoList.Add(processorInfo);

            return processorInfoList;

        }

        private void ParseProcessorLinesbetweenProcessorTags(string currline = "")
        {
            ProcessorInfo processorInfo = new ProcessorInfo();
            //string currline = lstConfig[intLineNumTracker].Trim();

            if (currline.Trim().EndsWith("/>") || currline.Trim().EndsWith("</processor>"))
            {
                processorInfoList = AddProcessorInfofromLine(processorInfo, currline);

            }
            else
            {
                if (lstConfig[intLineNumTracker].Trim().EndsWith(">"))
                {
                    do
                    {
                        if (lstConfig[intLineNumTracker].Trim().StartsWith("<processor"))
                            processorInfoList = AddProcessorInfofromLine(processorInfo, currline);

                        intLineNumTracker++;

                    } while (lstConfig[intLineNumTracker].Trim() != "</processor>");
                }
                else
                {
                    //if line continues to next
                    var straightenedline = StraightenLine(intLineNumTracker);

                    processorInfoList = AddProcessorInfofromLine(processorInfo, straightenedline);
                }
            }

        }

        private bool ProcessorLineCommented(string singleCommentedline)
        {
            if (singleCommentedline.Trim().StartsWith("<!--") && singleCommentedline.Trim().EndsWith("-->"))
            {
                if (singleCommentedline.Contains("<processor "))
                {
                    processorTagCommented = true;
                    return true;
                }
            }

            return false;
        }

        private int GetValidLineNumber(int inttmplinetracker)
        {
            string processorLine = string.Empty;
            string currline;
            do
            {
                currline = lstConfig[inttmplinetracker].Trim();

                processorLine += currline + " ";

                inttmplinetracker--;

            } while (GetLeft(currline, 4) != "<!--");

            return inttmplinetracker;
        }

        private string StraightenLine(int templinetracker)
            {
                string processorLine = string.Empty;
                string currline;
                do
                {
                    currline = lstConfig[intLineNumTracker].Trim();

                    processorLine += currline + " ";

                    intLineNumTracker++;

                } while (GetRight(currline, 1) != ">");

                intLineNumTracker = intLineNumTracker - 1;

                return processorLine;
            }

            private string StraightenLinefromRight(int templinetracker)
            {
                string commentedLine = string.Empty;
                string currline;
                do
                {
                    currline = lstConfig[templinetracker].Trim();
                    commentedLine = currline + " " + commentedLine;

                    templinetracker--;

                } while (GetLeft(currline, 4) != "<!--");

                return commentedLine;
            }

            private string StraightenLinefromLeft()
            {
                string commentedLine = string.Empty;
                string currline;
                do
                {
                    currline = lstConfig[intLineNumTracker].Trim();
                    commentedLine += " " + currline;

                    intLineNumTracker++;

                } while (GetRight(currline, 3) != "-->");

                return commentedLine;
            }


            private string StraightenProcessorLinefromRight(int templinetracker)
            {
                if (string.IsNullOrWhiteSpace(lstConfig[templinetracker].Trim())) return string.Empty;

                string processorLine = string.Empty;
                string currline;
                do
                {
                    currline = lstConfig[templinetracker].Trim();
                    processorLine = currline + " " + processorLine;

                    templinetracker--;

                } while (GetLeft(currline, 10) != "<processor");

                return processorLine;
            }

            private string GetRight(string original, int numberCharacters)
            {
                if (string.IsNullOrWhiteSpace(original) || original.Length < numberCharacters) return string.Empty;

                return original.Trim().Substring(original.Length - numberCharacters);
            }

            private string GetLeft(string original, int numberCharacters)
            {
                if (string.IsNullOrWhiteSpace(original) || original.Length < numberCharacters) return string.Empty;

                return original.Trim().Substring(0, numberCharacters);
            }

            private void button2_Click(object sender, EventArgs e)
            {
                var ext = new List<string> { "config" };
                var configFiles = Directory
                   .EnumerateFiles(txtSelectedPath.Text, "*.config", SearchOption.AllDirectories)
                   .Where(s => ext.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()));

                keywordBeginTag = "<pipelines>";
                keywordClosingTag = keywordBeginTag.Replace("<", "</");

                pipelineInfoList = new List<PipelineInfo>();
                lineRange = new LineRange();

                processorSerialNumber = 0;//reset during every click

                foreach (var filePath in configFiles)
                    {
                        if (!(filePath.ToLowerInvariant().StartsWith("web.") || filePath.ToLowerInvariant().EndsWith(".disabled")))
                        {
                            string configFileData = File.ReadAllText(filePath);
                            if (string.IsNullOrWhiteSpace(configFileData)) continue;

                            if (configFileData.Contains("<processor"))
                            {
                                GetLineNumberRange(configFileData);
                                ParseFile(filePath, keywordBeginTag);
                            }
                        }
                    }

                if (pipelineInfoList.Count > 0)
                    if (flag == "pipeline") { SavePipelineHtml(); } else { SaveAllHtml(); }


            }

            private void SaveProcessorHtml()
            {
                string concatenatedLines = string.Empty;

                //concatenatedLines += "\n<html>\r";
                concatenatedLines += "\n<p align=center>Sitecore Processor List</p>\r";
                concatenatedLines += "\n<tr><td>S.No.</td><td>Name</td><td>Type</td><td>Method</td><td>Comment</td>\r";

                foreach (var pipelineInfo in pipelineInfoList) {

                    //concatenatedLines += $"\n\r\n\t<tr>\n\r\n\t\t<td colspan=4><b>{pipelineInfo.Name}</b></td></tr>";
                    //concatenatedLines += $"\n<tr><td colspan=4>{pipelineInfo.Comment}</td></tr>";

                    foreach (var processor in pipelineInfo.ProcessorInfoList)
                    {
                        concatenatedLines += $"\n<tr><td>{processor.SerialNumber}</td><td>{processor.Name}</td><td>{processor.Type}</td><td>{processor.Method}</td><td>{processor.Comment}</td></tr>";
                    }
                }

                File.WriteAllText("./SitecoreProcessorlist.html", concatenatedLines);
            }

        private void SaveAllHtml()
        {
            string concatenatedLines = string.Empty;

            //concatenatedLines += "\n<html>\r";
            concatenatedLines += "\n<p align=center>Sitecore Pipeline Processor List</p>\r";
            concatenatedLines += "\n<tr><td>S.No.</td><td>Pipeline</td><td>Processor</td><td>Type</td><td>Method</td>\r";

            foreach (var pipelineInfo in pipelineInfoList)
            {

                //concatenatedLines += $"\n\r\n\t<tr>\n\r\n\t\t<td colspan=4><b>{pipelineInfo.Name}</b></td></tr>";
                //concatenatedLines += $"\n<tr><td colspan=4>{pipelineInfo.Comment}</td></tr>";

                foreach (var processor in pipelineInfo.ProcessorInfoList)
                {
                    concatenatedLines += $"\n<tr><td>{processor.SerialNumber}</td><td>{pipelineInfo.Name}</td><td>{processor.Name}</td><td>{processor.Type}</td><td>{processor.Method}</td></tr>";
                }
            }

            File.WriteAllText("./SitecorePipelineProcessorlist.html", concatenatedLines);
        }

        private void SaveHtml()
            {
                string concatenatedLines = string.Empty;

                //concatenatedLines += "\n<html>\r";
                concatenatedLines += "\n<p align=center>Sitecore Processor List</p>\r";
                concatenatedLines += "\n<tr><td>Name</td><td>Type</td><td>Method</td><td>Comment</td>\r";

                foreach (var pipelineInfo in pipelineInfoList)
                {

                    concatenatedLines += $"\n\r\n\t<tr>\n\r\n\t\t<td colspan=4><b>{pipelineInfo.Name}</b></td></tr>";
                    //concatenatedLines += $"\n<tr><td colspan=4>{pipelineInfo.Comment}</td></tr>";

                    foreach (var processor in pipelineInfo.ProcessorInfoList)
                    {
                        concatenatedLines += $"\n<tr><td>{processor.Name}</td><td>{processor.Type}</td><td>{processor.Method}</td><td>{processor.Comment}</td></tr>";
                    }
                }

                File.WriteAllText("./SitecoreProcessorlist.html", concatenatedLines);
            }

            private void SavePipelineHtml()
            {
                string concatenatedLines = string.Empty;

                //concatenatedLines += "\n<html>\r";
                concatenatedLines += "\n<p align=center>Sitecore Pipeline List</p>\r";
                concatenatedLines += "\n<tr><td>Name</td><td>File Name</td><td>Comment</td></tr>\r";

                foreach (var pipelineInfo in pipelineInfoList)
                {

                    concatenatedLines += $"\n\r\t<tr>\n\r\t\t<td><b>{pipelineInfo.Name}</b></td>\n\r\t\t<td>{pipelineInfo.FileName}</td>\n\r\t\t<td>{pipelineInfo.Comment}</td>\n\r\t</tr>";
                    //concatenatedLines += $"\n<tr><td colspan=4>{pipelineInfo.Comment}</td></tr>";

                }

                File.WriteAllText("./SitecorePipelinelist.html", concatenatedLines);
            }


            private void GetLineNumberRange(string configData)
            {
                string[] lstConfig = configData.Split(new Char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                int intLineNumTrackerIndex = 0;

                foreach (var line in lstConfig)
                {
                    if (line.ToLowerInvariant().Contains("<pipelines>"))
                    {
                       if (intLineNumTrackerIndex< lineRange.StartLineIndex) lineRange.StartLineIndex = intLineNumTrackerIndex; //since there could be multiple pipelines tags
                    }

                    if (line.ToLowerInvariant().Contains(".pipelines.") && line.ToLowerInvariant().Contains("<processor"))
                    {
                        if (intLineNumTrackerIndex < lineRange.StartLineIndex)
                        {
                            lineRange.StartLineIndex = intLineNumTrackerIndex;
                        }
                    }

                    if (line.ToLowerInvariant().Contains("</pipelines>"))
                    {
                        if (intLineNumTrackerIndex> lineRange.EndLineIndex) lineRange.EndLineIndex = intLineNumTrackerIndex;
                    }

                    if (line.ToLowerInvariant().Contains("</processors>") && intLineNumTrackerIndex > lineRange.EndLineIndex)
                    {
                        lineRange.EndLineIndex = intLineNumTrackerIndex;
                    }

                    intLineNumTrackerIndex += 1;
                }
            }
     }
 }

