using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace ConfigFileExtractor
{
    internal class Program
    {
        private static string parsePath = "C:\\inetpub\\wwwroot\\sc104siasc.dev.local\\App_Config";
        private static string[] lstConfig;
        private static LineRange lineRange;
        private static string configFileData;
        private static List<NodeInfo> nodeInfoList;
        private static List<LeafInfo> leafInfoList;
        private static int intLineNumTracker = 0;
        private static string previousClosingTag = "dummy";
        private static bool tagOpened;
        static bool leafTagCommented = false;
        private static int leafSerialNumber = 0;
        private static bool nodeCommentAdded = false;
        private static int parseType;
        private static string leafTagString;
        private static string nodeTagString;
        private static string searchTagStringList;
        private static string[] searchStringArray;
        private static int lastTagOccurence;
        private static int outputType;
        private static int inputOption = 0;
        private static string htmlOpenTag = "<html><table>";
        private static string htmlCloseTag = "</table></html>";
        private static bool isLeaflevel = false;

        static void Main(string[] args)
        {
            //flag = "pipeline";

            //args:
            //path
            //type - leaf level extraction - 1
            //line range - start and end tag string - <settings> and </settings>
            // retrieve node - false
            //leaftag - like <setting 

            //type - node and leaf level extraction - 2
            //path
            //line range - start and end tag string - <pipelines> and </processors>
            // retrieve node - true
            //node tag - 
            //leaftag - <processor 
            //child tag attributes to retrieve - type, method, resolve

            //
            Console.Title = "Sitecore Config File Parser";
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Enter the folder path to search: ");
            Console.ForegroundColor = ConsoleColor.White;
            parsePath = Console.ReadLine();

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("\rEnter number for the option:\r");
            Console.WriteLine("1. Pipeline List\r");
            Console.WriteLine("2. Pipeline Processor List\r");
            Console.WriteLine("3. Event List\r");
            Console.WriteLine("4. Event handler List\r");
            Console.WriteLine("5. Settings List\r");
            Console.WriteLine("6. Command List\r");
            Console.ForegroundColor = ConsoleColor.White;
            inputOption = Convert.ToInt16(Console.ReadLine());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\rEnter number for the output type:\r");
            Console.WriteLine("1. HTML\r");
            Console.WriteLine("2. CSV\r");
            Console.ForegroundColor = ConsoleColor.White;
            outputType = Convert.ToInt16(Console.ReadLine());

            switch (inputOption)
            {
                case 1:
                    //node extraction
                    parseType = 2;
                    searchTagStringList = "pipelines,processors";
                    lastTagOccurence = 1;
                    leafTagString = "processor";
                    break;
                case 2:
                    parseType = 2;
                    searchTagStringList = "pipelines,processors";
                    lastTagOccurence = 1;
                    isLeaflevel = true;
                    leafTagString = "processor";
                    break;
                case 3:
                    //node extraction
                    parseType = 2;
                    searchTagStringList = "events";
                    lastTagOccurence = 0;
                    nodeTagString = "event";
                    leafTagString = "handler";
                    break;
                case 4:
                    //leaf extraction
                    parseType = 2;
                    searchTagStringList = "events";
                    lastTagOccurence = 0;
                    nodeTagString = "event";
                    leafTagString = "handler";
                    isLeaflevel = true;
                    break;
                case 5:
                    parseType = 1;
                    searchTagStringList = "settings";
                    lastTagOccurence = 0;
                    leafTagString = "setting";
                    break;
                case 6:
                    parseType = 1;
                    searchTagStringList = "commands";
                    lastTagOccurence = 0;
                    leafTagString = "command";
                    break;
            }

            if (string.IsNullOrWhiteSpace(searchTagStringList)) return;

            if (searchTagStringList.Contains(",")) 
            { 
                searchStringArray = searchTagStringList.Split(','); 
            }
            else
            {
                searchTagStringList += ",";
                searchStringArray = searchTagStringList.Split(',');
            }

            ProcessConfigFile();
            Console.WriteLine("Done");
            
        }

        private static string ExtractString(string originalString, string firstString, string nextString)
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
            internal string TagString { get; set; }
        }

        internal class NodeInfo
        {
            internal string Comment { get; set; }
            internal string Name { get; set; }
            internal string FileName { get; set; }
            internal int SerialNumber { get; set; }
            internal List<LeafInfo> LeafInfoList { get; set; }
        }

        internal class LeafInfo
        {
            internal string Type { get; set; }
            internal string Method { get; set; }
            internal string Name { get; set; }
            internal string Value { get; set; }
            internal string Comment { get; set; }
            internal int SerialNumber { get; set; }
        }

        private static string[] ExtractArraywithSplit(string content, string keyword)
        {
            return content.Split(new string[] { keyword }, StringSplitOptions.None);
        }

        private static string GetComment(string line)
        {
            string comment = string.Empty;

            if (line.Trim().EndsWith("-->"))
            {
                if (line.Trim().StartsWith("<!--"))
                {
                    comment = ExtractString(line.Trim(), "<!--", "-->");
                }
            }

            return comment;
        }

        private static void NormalizeArray()
        {
            //remove blank lines
            //straighten lines
            string[] lineList = lstConfig;
            List<string> newList = new List<string>();

            for (intLineNumTracker = 0; intLineNumTracker < lineList.Length; intLineNumTracker++)
            {
                if (!string.IsNullOrWhiteSpace(lineList[intLineNumTracker].Trim()))
                {
                    string newline = lineList[intLineNumTracker];

                    //two types of lines - uncommented and commented line to be straightened

                    if (newline.Trim().Substring(0, 4) == "<!--")
                    {
                        if (GetRight(newline, 3) != "-->") newline = StraightenCommentedLine(); //commented line
                    }
                    else
                    {
                        if (GetRight(newline, 1) != ">") newline = StraightenLine(); //uncommented line
                    }

                    newList.Add(newline);
                }

            }

            lstConfig = newList.ToArray();

        }

        private static void MultipleCommentslinesasoneLineArray()
        {
            //consolidate comments in one line
            string[] lineList = lstConfig;
            List<string> newList = new List<string>();

            for (intLineNumTracker = 0; intLineNumTracker < lineList.Length; intLineNumTracker++)
            {
                string newline = lineList[intLineNumTracker];

                if (GetRight(newline.Trim(), 3) == "-->") newline = ConsolidateComments();

                newList.Add(newline);
            }

            lstConfig = newList.ToArray();
        }

        private static string ConsolidateComments()
        {
            var consolidatedComments = string.Empty;

            do
            {
                consolidatedComments += lstConfig[intLineNumTracker];

                intLineNumTracker++;

            } while (lstConfig[intLineNumTracker].Trim().StartsWith("<!--"));

            intLineNumTracker--;

            return consolidatedComments;
        }

        private static void ParseFile(string filePath, string parentkeyword)
        {
            configFileData = File.ReadAllText(filePath);
            NodeInfo nodeInfo = new NodeInfo();

            if (!(configFileData.ToLowerInvariant().Contains(parentkeyword)))
                return;//no need to parse

            string strConfigText = configFileData;
            lstConfig = strConfigText.Split(new Char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            //normalize array before any processing
            NormalizeArray();
            //next, if there are single comment tags consecutively, then put it as part of one comment tag
            MultipleCommentslinesasoneLineArray();

            GetStartandEndLineIndex(parentkeyword);//all set now, get the startlineindex and endlineindex array since it will be accurate
            
            if (lineRange.StartLineIndex <= 0 || lineRange.EndLineIndex <= 0) return;

            leafInfoList = new List<LeafInfo>();
            for (intLineNumTracker = lineRange.StartLineIndex; intLineNumTracker <= lineRange.EndLineIndex; intLineNumTracker++)
            {
                var currentLine = lstConfig[intLineNumTracker];
                var openTagLine = lstConfig[intLineNumTracker - 1];
                //filter some of the keywords here since they are not valid tags to retrieve
                if (currentLine.Trim().StartsWith("<add ") || currentLine.Trim().StartsWith("<request ")) continue;

                //logic specific to event
                if (inputOption == 3 || inputOption == 4) if (currentLine.Trim().StartsWith("<processor ")) continue; //since event doesn't need to parse this - like, Sitecore.ExperienceProfile.Reporting.config

                leafTagCommented = LeafLineCommented(currentLine);

                int tmpprevlinenum = intLineNumTracker - 1;
                var tmpprevline = lstConfig[tmpprevlinenum];

                string leafCommentLine = string.Empty;
                string previousLine = lstConfig[tmpprevlinenum];//if no leaf comments exist

                string nodecommentline = string.Empty;

                if (tmpprevline.Trim().StartsWith("<!--"))
                {
                    leafCommentLine = lstConfig[tmpprevlinenum];
                    previousLine = lstConfig[tmpprevlinenum - 1];
                }

                string nextLine=string.Empty;
                if (intLineNumTracker< lstConfig.Length-1) nextLine = lstConfig[intLineNumTracker + 1];
                
                //nodes falling in level 1 or 2
                //there could be scenarios when node is opened and closed without any leaves within like, <nodetag /> and this must be handled too
                if (!currentLine.Trim().StartsWith("<!--") && !currentLine.Trim().StartsWith($"<{leafTagString}") && !isLeaflevel)//node lines enter this block
                {
                    string tmpCurrentLine;
                    if (currentLine.Trim().Contains(' ')) tmpCurrentLine = currentLine.Trim().Split(' ')[0]; else tmpCurrentLine = currentLine.Trim();

                    if (!string.IsNullOrWhiteSpace(nodeTagString) && currentLine.Trim().StartsWith($"<{nodeTagString} ") || RemoveSpecialCharacters(tmpCurrentLine) == RemoveSpecialCharacters(nextLine.Trim()) || currentLine.Trim().EndsWith("/>"))
                    {
                        //1. because fixed tag nodes without children must be accounted
                        //2. tag starts and ends in one line
                        //3. next line is close node tag
                        leafTagCommented = true;

                        //now extract node comment just above node open tag
                        if (!nodeCommentAdded)
                            if (lstConfig[tmpprevlinenum].Trim().StartsWith("<!--")) nodecommentline = lstConfig[tmpprevlinenum];

                        //node info extraction
                        string comment = nodecommentline.Replace("<!--", string.Empty).Replace("-->", string.Empty);

                        if (currentLine.Trim().Contains(" help=")) //this line contains node help
                            comment += " " + ExtractArraywithSplit(currentLine.Trim(), " help=")[1];

                        string name = string.Empty;

                        if (currentLine.Contains(" name=")) name = ExtractArraywithSplit(currentLine, " name=")[1];
                        if (name.Contains(" help=")) name = ExtractArraywithSplit(name, " help=")[0];

                        //logic specific to event                       
                        if (currentLine.Trim().StartsWith("<event "))
                        {
                            if (nextLine.Trim().Contains("attributeName>")) name = ExtractString(nextLine, ">", "</");
                        }
                        //logic specific to event

                        nodeInfo = new NodeInfo
                        {
                            Comment = comment.Replace("<!--", string.Empty).Replace("-->", string.Empty).Replace(">", string.Empty).Replace("\"", string.Empty).Trim().Trim()
                        };

                        if (string.IsNullOrWhiteSpace(name)) name = ExtractString(currentLine, "<", ">").Split(' ')[0];
                        nodeInfo.Name = RemoveSpecialCharacters(name);

                        nodeCommentAdded = true;
                        //node info extraction

                        var pathSplit = ExtractArraywithSplit(filePath, "\\");
                        nodeInfo.FileName = pathSplit[pathSplit.Length - 1];

                        //leaf tags within an existing node
                        if (!leafTagCommented) ParseLeafLinesbetweenLeafTags(currentLine);

                        if (currentLine.Trim().Replace("<", "</") == nextLine.Trim()) intLineNumTracker++; //this is already accounted since node close tag is just after open tag

                        tagOpened = true;
                    }
                }

                if (currentLine.Trim().StartsWith($"<{leafTagString} ") || openTagLine.Trim().StartsWith(currentLine.Trim().Replace("/>", "<")))//leaf lines enter this block
                {
                    if (openTagLine.Trim().StartsWith(currentLine.Trim().Replace("</", "<"))) leafTagCommented = true;//since close tag is just after open tag 
                                                                                                                        //first leaf tag enters here and gets the node info too
                                                                                                                        //retrieve prev line                    
                    if (tmpprevline.Trim().StartsWith("<!--"))
                    {
                        leafCommentLine = lstConfig[tmpprevlinenum];
                        previousLine = lstConfig[tmpprevlinenum - 1];
                    }

                    //now extract node comment 
                    if (parseType == 2) //applicable only for more than one level nesting
                    {
                        if (!nodeCommentAdded)
                        {
                            if (string.IsNullOrWhiteSpace(leafCommentLine))
                            {
                                if (lstConfig[tmpprevlinenum - 1].Trim().StartsWith("<!--")) nodecommentline = lstConfig[tmpprevlinenum - 1];
                            }
                            else
                            {
                                if (lstConfig[tmpprevlinenum - 2].Trim().StartsWith("<!--")) nodecommentline = lstConfig[tmpprevlinenum - 2];
                            }
                        }

                        //now check if prev line isn't anything else 
                        if (!previousLine.Trim().StartsWith($"<{leafTagString}") && !previousLine.Trim().StartsWith($"</{leafTagString}"))
                        {
                            //node info extraction
                            string comment = nodecommentline.Replace("<!--", string.Empty).Replace("-->", string.Empty);

                            if (previousLine.Trim().Contains(" help=")) //this line contains node help
                                comment += " " + ExtractArraywithSplit(previousLine.Trim(), " help=")[1];

                            string name = string.Empty;
                            if (previousLine.Contains(" name=")) name = ExtractArraywithSplit(previousLine, " name=")[1];

                            nodeInfo = new NodeInfo
                            {
                                Comment = comment.Replace("<!--", string.Empty).Replace("-->", string.Empty).Replace(">", string.Empty).Replace("\"", string.Empty).Trim().Trim()
                            };

                            if (string.IsNullOrWhiteSpace(name)) name = ExtractString(previousLine, "<", ">").Split(' ')[0];
                            nodeInfo.Name = name;

                            nodeCommentAdded = true;
                            //node info extraction

                        }
                    }
                    else
                    {
                        nodeInfo = new NodeInfo();
                    }

                    var pathSplit = ExtractArraywithSplit(filePath, "\\");
                    nodeInfo.FileName = pathSplit[pathSplit.Length - 1];

                    //leaf tags within an existing node
                    if (!leafTagCommented) ParseLeafLinesbetweenLeafTags(currentLine);

                    tagOpened = true;

                    if (openTagLine.Trim().StartsWith(currentLine.Trim().Replace("</", "<"))) AddtoListandReset(intLineNumTracker, nodeInfo, filePath);//since leaf close tag is just after open tag

                }
                else
                {
                    //all other lines enter this flow
                    AddtoListandReset(intLineNumTracker, nodeInfo, filePath);
                }

                leafTagCommented = false;
            }
            AddtoListandReset(intLineNumTracker - 1, nodeInfo, filePath);
        }

        private static void AddtoListandReset(int lineIndex, NodeInfo nodeInfo, string filePath)
        {
            switch (parseType)
            {
                case 1:
                    if (leafInfoList.Count > 0)
                    {
                        nodeInfo.LeafInfoList = leafInfoList;
                        nodeInfoList.Add(nodeInfo);
                        leafInfoList = new List<LeafInfo>();
                    }
                    break;
                case 2:
                case 0:
                    if (tagOpened)
                    {
                        string closingTag;

                        if (string.IsNullOrWhiteSpace(nodeTagString))
                            closingTag = $"</{nodeInfo.Name}>";
                        else
                            closingTag = $"</{nodeTagString}>";

                        var currentLine = lstConfig[lineIndex];

                        if (currentLine.Trim().StartsWith(closingTag) || currentLine.Trim().EndsWith("/>"))
                        {
                            nodeInfo.LeafInfoList = leafInfoList;
                            nodeInfoList.Add(nodeInfo);
                            leafInfoList = new List<LeafInfo>();
                            previousClosingTag = closingTag;
                            tagOpened = false;
                            nodeCommentAdded = false;
                        }
                    }
                    break;
                default:
                    break;
            }
            
        }

        private static string RemoveSpecialCharacters(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;

            name = name.Replace("\"", string.Empty);
            name = name.Replace(">", string.Empty);
            name = name.Replace("/", string.Empty);
            name = name.Replace("]", string.Empty);
            name = name.Replace("<", string.Empty);
            name = name.Replace("[", string.Empty);

            return name;
        }

        private static List<LeafInfo> AddLeafInfofromLine(LeafInfo leafInfo, string actualLine, string previousline = "")
        {
            string[] leaflineAttributes= { };
            if (!actualLine.Contains("value=")) leaflineAttributes = actualLine.Trim().Split(' ');//since value could have assembly details separated by space

            string type = string.Empty;
            string method = string.Empty;

            //might be useful  in future - https://www.w3schools.com/cs/cs_arrays_multi.php
            //extract processor info
            if (actualLine.Contains("type="))
            {
                //extraction logic is different for such lines
                string[] leafLineSplit = actualLine.Split(',');
                type = ExtractArraywithSplit(actualLine, "type=")[1];

                string[] splitType = leafLineSplit[0].Split('.');
                leafInfo.Name = splitType[splitType.Length - 1];

                if (type.Contains(" method=")) type = ExtractArraywithSplit(type, " method=")[0];
                if (type.Contains(" resolve="))
                    type = ExtractArraywithSplit(type, " resolve=")[0];
                if (type.Contains(" patch:")) type = ExtractArraywithSplit(type, " patch:")[0];
                if (type.Contains(" role:")) type = ExtractArraywithSplit(type, " role:")[0];
                if (type.Contains(" x:after=")) type = ExtractArraywithSplit(type, " x:after=")[0];
                if (type.Contains(" x:before=")) type = ExtractArraywithSplit(type, " x:before=")[0];
                if (type.Contains(", Version=")) type = ExtractArraywithSplit(type, ", Version=")[0];
                
                type = RemoveSpecialCharacters(type);
            }

            foreach (string str in leaflineAttributes)
            {
                if (str.ToLowerInvariant().Contains("name=")) leafInfo.Name = ExtractArraywithSplit(str, "name=")[1];
                //if (str.ToLowerInvariant().Contains("value=")) leafInfo.Value = ExtractArraywithSplit(str, "value=")[1].Replace("/>",string.Empty);

                if (str.ToLowerInvariant().Contains("method=")) method = ExtractArraywithSplit(str, "method=")[1];
            }


            string valueextract = string.Empty;
            string nameextract = string.Empty;

            if (inputOption == 5) //for settings
            {
                leafInfo.Name = RemoveSpecialCharacters(ExtractArraywithSplit(ExtractArraywithSplit(actualLine, " value=")[0], " name=")[1].Split(' ')[0]);

                if (actualLine.Contains("value="))
                    //extraction logic is different for such lines   
                    valueextract = ExtractArraywithSplit(actualLine, " value=")[1];

                if (actualLine.Contains(" role:require"))
                {
                    //leafInfo.Name = RemoveSpecialCharacters(ExtractArraywithSplit(ExtractArraywithSplit(actualLine, " value=")[0], " name=")[1]);

                    if (actualLine.Contains("value"))
                    {
                        valueextract = ExtractString(actualLine, "value=", " role:require=");
                    }
                    else
                    {
                        if (lstConfig[intLineNumTracker + 1].Contains("value")) valueextract = ExtractString(lstConfig[intLineNumTracker + 1], ">", "</");
                        intLineNumTracker++;
                    }

                    leafInfo.Comment += "role:require=" + ExtractArraywithSplit(actualLine, " role:require=")[1].Replace("/",string.Empty).Replace(">", string.Empty);
                }

                if (actualLine.Contains(" security:require"))
                {
                    if (actualLine.Contains("value"))
                    {
                        valueextract = ExtractString(actualLine, "value=", " security:require=");
                    }
                    else
                    {
                        if (lstConfig[intLineNumTracker + 1].Contains("value")) valueextract = ExtractString(lstConfig[intLineNumTracker + 1], ">", "</");
                        intLineNumTracker++;
                    }

                    leafInfo.Comment += "security:require=" + ExtractArraywithSplit(actualLine, " security:require=")[1].Replace("/", string.Empty).Replace(">", string.Empty);
                }

                leafInfo.Value = valueextract.Replace("/>", string.Empty).Trim();
            }

            leafSerialNumber += 1;
  
            leafInfo.Type = type;
            leafInfo.Method = RemoveSpecialCharacters(method);
            leafInfo.SerialNumber = leafSerialNumber;

            if (!string.IsNullOrWhiteSpace(lstConfig[intLineNumTracker - 1])) leafInfo.Comment 
                    += GetComment(lstConfig[intLineNumTracker - 1]);

            leafInfoList.Add(leafInfo);

            return leafInfoList;
        }

        private static void ParseLeafLinesbetweenLeafTags(string currline = "")
        {
            LeafInfo leafInfo = new LeafInfo();

            if (currline.Trim().EndsWith("/>") || currline.Trim().EndsWith($"</{leafTagString}>"))
            {
                leafInfoList = AddLeafInfofromLine(leafInfo, currline);

            }
            else
            {
                if (lstConfig[intLineNumTracker].Trim().EndsWith(">"))
                {
                    do
                    {
                        if (lstConfig[intLineNumTracker].Trim().StartsWith($"<{leafTagString}"))
                            leafInfoList = AddLeafInfofromLine(leafInfo, currline);

                        intLineNumTracker++;

                    } while (lstConfig[intLineNumTracker].Trim() != $"</{leafTagString}>");
                }
                else
                {
                    leafInfoList = AddLeafInfofromLine(leafInfo, lstConfig[intLineNumTracker]);
                }
            }

        }

        private static bool LeafLineCommented(string singleCommentedline)
        {
            if (singleCommentedline.Trim().StartsWith("<!--") && singleCommentedline.Trim().EndsWith("-->"))
            {
                if (singleCommentedline.Contains($"<{leafTagString} "))//since there could be attributes following the tag itself so a space after the tag
                {
                    leafTagCommented = true;
                    return true;
                }
            }

            return false;
        }

        private static string StraightenLine()
        {
            string leafLine = string.Empty;
            string currline;

            do
            {
                currline = lstConfig[intLineNumTracker].Trim();

                leafLine += currline + " ";

                intLineNumTracker++;

            } while (GetRight(currline, 1) != ">");

            intLineNumTracker--;

            return leafLine;
        }

        private static string StraightenCommentedLine()
        {
            string leafLine = string.Empty;
            string currline;

            do
            {
                currline = lstConfig[intLineNumTracker].Trim();

                leafLine += currline + " ";

                intLineNumTracker++;

            } while (GetRight(currline, 3) != "-->");

            intLineNumTracker--;

            return leafLine;
        }

        private static string GetRight(string original, int numberCharacters)
        {
            if (string.IsNullOrWhiteSpace(original) || original.Length < numberCharacters) return string.Empty;

            return original.Substring(original.Length - numberCharacters);
        }

        private static string GetLeft(string original, int numberCharacters)
        {
            if (string.IsNullOrWhiteSpace(original) || original.Length < numberCharacters) return string.Empty;

            return original.Trim().Substring(0, numberCharacters);
        }

        private static void ProcessConfigFile()
        {
            var ext = new List<string> { "config" };
            var configFiles = Directory
                .EnumerateFiles(parsePath, "*.config", SearchOption.AllDirectories)
                .Where(s => ext.Contains(Path.GetExtension(s).TrimStart('.').ToLowerInvariant()));

            nodeInfoList = new List<NodeInfo>();
            lineRange = new LineRange();

            leafSerialNumber = 0;//reset during every run

            foreach (var filePath in configFiles)
            {
                foreach (string searchString in searchStringArray)
                {
                    string keywordEndTag = $"</{searchString}>";

                    if (!(filePath.ToLowerInvariant().StartsWith("web.") || filePath.ToLowerInvariant().EndsWith(".disabled")))
                    {
                        string configFileData = File.ReadAllText(filePath);
                        if (string.IsNullOrWhiteSpace(configFileData)) continue;

                        if (configFileData.Contains(keywordEndTag))//definitely begin tag must be there then
                            ParseFile(filePath, searchString);
                    }
                }
            }
           
            if (nodeInfoList.Count <= 0) return;
            switch (inputOption)
            {
                case 1:
                    switch (outputType)
                    {
                        case 1:
                            SavePipelineHtml();                            
                            break;
                        case 2:
                            SavePipelinecsv();
                            break;
                    }
                    break;
                case 2:
                    switch (outputType)
                    {
                        case 1:
                            SavePipelineProcessorHtml();
                            break;
                        case 2:
                            SavePipelineProcessorcsv();
                            break;
                    }
                    break;
                case 3:
                    switch (outputType)
                    {
                        case 1:
                            SaveEventListHtml();
                            break;
                        case 2:
                            //SaveEventListcsv();
                            break;
                    }
                    break;
                case 4:
                    switch (outputType)
                    {
                        case 1:
                            SaveEventHandlerListHtml();
                            break;
                        case 2:
                            //SaveEventHandlerListcsv();
                            break;
                    }
                    break;
                case 5:
                    switch (outputType)
                    {
                        case 1:
                            SaveSettingsListHtml();
                            break;
                        case 2:
                            //SaveSettingsListcsv();
                            break;
                    }
                    break;
                case 6:
                    switch (outputType)
                    {
                        case 1:
                            SaveCommandListHtml();
                            break;
                        case 2:
                            //SaveCommandListcsv();
                            break;
                    }
                    break;
            }
           
        }

        private static void SaveLeafHtml()
        {
            string concatenatedLines = string.Empty;

            concatenatedLines += $"\n<p align=center>Sitecore {leafTagString} list</p>\r";
            concatenatedLines += $"\n<tr><td>S.No.</td><td>File Name</td><td>{leafTagString}</td><td>Comment</td></tr>\r";

            foreach (var nodeInfo in nodeInfoList)
            {
                foreach (var leaf in nodeInfo.LeafInfoList)
                {
                    concatenatedLines += $"\n<tr><td>{leaf.SerialNumber}</td><td>{nodeInfo.FileName}</td><td>{leaf.Name}</td><td>{leaf.Comment.Trim()}</td></tr>";
                }
            }

            File.WriteAllText($"./Sitecore{leafTagString}list.html", concatenatedLines);
        }

        private static void SaveNodeandLeafHtml()
        {
            string concatenatedLines = string.Empty;

            concatenatedLines += $"\n<p align=center>Sitecore {leafTagString} list</p>\r";
            concatenatedLines += $"\n<tr><td>S.No.</td><td>File Name</td><td></td><td>{leafTagString}</td><td>Type</td><td>Method</td><td>Comment</td></tr>\r";
            int intsno = 0;

            foreach (var nodeInfo in nodeInfoList)
            {
                if (nodeInfo.LeafInfoList.Any())
                {
                    foreach (var leaf in nodeInfo.LeafInfoList)
                    {
                        intsno++;
                        concatenatedLines += $"\n<tr><td>{intsno}</td><td>{nodeInfo.FileName}</td><td>{nodeInfo.Name}</td><td>{leaf.Name}</td><td>{leaf.Type}</td><td>{leaf.Method}</td><td>{leaf.Comment}</td></tr>";
                    }
                }
                else
                {
                    intsno++;
                    concatenatedLines += $"\n<tr><td>{intsno}</td><td>{nodeInfo.FileName}</td><td>{nodeInfo.Name}</td><td></td><td></td><td></td></tr>";
                }
            }

            File.WriteAllText($"./Sitecore{leafTagString}list.html", concatenatedLines);
        }

        private static void SavePipelineProcessorHtml()
        {
            string concatenatedLines = string.Empty;

            concatenatedLines += $"{htmlOpenTag}\n<p align=center>Sitecore Pipeline Processor list</p>\r";
            concatenatedLines += $"\n<tr><th>S.No.</th><th>File Name</th><th>Pipeline</th><th>Processor</th><th>Type</th><th>Method</th></tr>\r";
            int intsno = 0;

            foreach (var nodeInfo in nodeInfoList)
            {
                if (nodeInfo.LeafInfoList.Any())
                {
                    foreach (var leaf in nodeInfo.LeafInfoList)
                    {
                        intsno++;
                        concatenatedLines += $"\n<tr><td>{intsno}</td><td>{nodeInfo.FileName}</td><td>{nodeInfo.Name}</td><td>{leaf.Name}</td><td>{leaf.Type}</td><td>{leaf.Method}</td></tr>";
                    }
                }
                else
                {
                    intsno++;
                    concatenatedLines += $"\n<tr><td>{intsno}</td><td>{nodeInfo.FileName}</td><td>{nodeInfo.Name}</td><td></td><td></td><td></td></tr>";
                }
            }

            concatenatedLines += htmlCloseTag;
            File.WriteAllText($"./Sitecore{leafTagString}list.html", concatenatedLines);
        }

        private static void SavePipelineProcessorcsv()
        {
            string concatenatedLines = string.Empty;

            concatenatedLines += $"\nS.No.,File Name,Pipeline,Processor,Type,Method,Comment\r";
            int intsno = 0;

            foreach (var nodeInfo in nodeInfoList)
            {
                if (nodeInfo.LeafInfoList.Any())
                {
                    foreach (var leaf in nodeInfo.LeafInfoList)
                    {
                        intsno++;
                        concatenatedLines += $"\n{intsno},{nodeInfo.FileName},{nodeInfo.Name},{leaf.Name},{leaf.Type},{leaf.Method},{leaf.Comment}";
                    }
                }
                else
                {
                    intsno++;
                    concatenatedLines += $"\n{intsno},{nodeInfo.FileName},{nodeInfo.Name},,,";
                }
            }

            File.WriteAllText($"./SitecoreProcessorlist.csv", concatenatedLines);
        }

        private static void SaveEventHandlerListHtml()
        {
            string concatenatedLines = string.Empty;

            concatenatedLines += $"{htmlOpenTag}\n<p align=center>Sitecore Event Handler list</p>\r";
            concatenatedLines += $"\n<tr><th>S.No.</th><th>File Name</th><th>Event Name</th><th>Handler Type</th><th>Method</th><th>Comment</th></tr>\r";

            int sno = 0;
            foreach (var nodeInfo in nodeInfoList)
            {
                if (nodeInfo.LeafInfoList.Any())
                {
                    foreach (var leaf in nodeInfo.LeafInfoList)
                    {
                        sno++;                        
                        concatenatedLines += $"\n<tr><td>{sno}</td><td>{nodeInfo.FileName}</td><td>{RemoveSpecialCharacters(nodeInfo.Name)}</td><td>{leaf.Type}</td><td>{RemoveSpecialCharacters(leaf.Method)}</td><td>{leaf.Comment}</td></tr>";
                    }
                }
                else
                {
                    sno++;
                    concatenatedLines += $"\n<tr><td>{sno}</td><td>{nodeInfo.FileName}</td><td>{RemoveSpecialCharacters(nodeInfo.Name)}</td><td></td><td></td><td></td</tr>";
                }
            }

            concatenatedLines += htmlCloseTag;
            File.WriteAllText($"./SitecoreEventhandlerlist.html", concatenatedLines);
        }

        private static void SaveEventListHtml()
        {
            string concatenatedLines = string.Empty;

            concatenatedLines += $"{htmlOpenTag}\n<p align=center>Sitecore Event list</p>\r";
            concatenatedLines += $"\n<tr><th>S.No.</th><th>File Name</th><th>Event Name</th><th>Comment</th></tr>\r";

            int sno = 0;
            foreach (var nodeInfo in nodeInfoList)
            {                
                sno++;
                concatenatedLines += $"\n<tr><td>{sno}</td><td>{nodeInfo.FileName}</td><td>{RemoveSpecialCharacters(nodeInfo.Name)}</td><td>{nodeInfo.Comment}</td></tr>";
            }

            concatenatedLines += htmlCloseTag;
            File.WriteAllText($"./SitecoreEventlist.html", concatenatedLines);
        }

        private static void SaveSettingsListHtml()
        {
            string concatenatedLines = string.Empty;

            concatenatedLines += $"{htmlOpenTag}\n<p align=center>Sitecore Settings list</p>\r";
            concatenatedLines += $"\n<tr><th>S.No.</th><th>Comment</th><th>Setting</th><th>Value</th></tr>\r";

            int sno = 0;
            foreach (var nodeInfo in nodeInfoList)
            {
                if (nodeInfo.LeafInfoList.Any())
                {
                    foreach (var leaf in nodeInfo.LeafInfoList)
                    {
                        sno++;
                        concatenatedLines += $"\n<tr><td>{sno}</td><td>{leaf.Comment.Trim()}</td><td>{RemoveSpecialCharacters(leaf.Name)}</td><td>{leaf.Value}</td></tr>";
                    }
                }
            }

            concatenatedLines += htmlCloseTag;
            File.WriteAllText($"./SitecoreSettingslist.html", concatenatedLines);
        }

        private static void SaveCommandListHtml()
        {
            string concatenatedLines = string.Empty;

            concatenatedLines += $"{htmlOpenTag}\n<p align=center>Sitecore Settings list</p>\r";
            concatenatedLines += $"\n<tr><th>S.No.</th><th>File Name</th><th>Command</th><th>Type</th><th>Comment</th></tr>\r";

            int sno = 0;
            foreach (var nodeInfo in nodeInfoList)
            {
                if (nodeInfo.LeafInfoList.Any())
                {
                    foreach (var leaf in nodeInfo.LeafInfoList)
                    {
                        sno++;
                        concatenatedLines += $"\n<tr><td>{sno}</td><td>{nodeInfo.FileName}</td><td>{RemoveSpecialCharacters(leaf.Name)}</td><td>{leaf.Type}</td><td>{leaf.Comment}</td></tr>";
                    }
                }
            }

            concatenatedLines += htmlCloseTag;
            File.WriteAllText($"./SitecoreCommandlist.html", concatenatedLines);
        }

        private static void SavePipelineHtml()
        {
            string concatenatedLines = string.Empty;

            concatenatedLines += $"{htmlOpenTag}\n<p align=center>Sitecore Pipeline list</p>\r";
            concatenatedLines += $"\n<tr><th>S.No.</th><th>Pipeline</th><th>File Name</th><th>Comment</th></tr>\r";

            int sno = 1;
            foreach (var nodeInfo in nodeInfoList)
            {
                
                concatenatedLines += $"\n<tr><td>{sno}</td><td>{RemoveSpecialCharacters(nodeInfo.Name)}</td><td>{nodeInfo.FileName}</td><td>{nodeInfo.Comment}</td></tr>";
                sno++;

            }

            concatenatedLines +=htmlCloseTag;
            File.WriteAllText($"./SitecorePipelinelist.html", concatenatedLines);
        }

        private static void SavePipelinecsv()
        {
            string concatenatedLines = string.Empty;

            concatenatedLines += $"\nS.No.,File Name,Pipeline,Comment\r";

            int sno = 1;
            foreach (var nodeInfo in nodeInfoList)
            {

                concatenatedLines += $"\n{sno},{nodeInfo.FileName},{nodeInfo.Name},{nodeInfo.Comment}";
                sno++;

            }
            File.WriteAllText($"./SitecorePipelinelist.csv", concatenatedLines);
        }

        private static void GetStartandEndLineIndex(string searchString)
        {
            int intLineTrackerIndex = 0;
            lineRange = new LineRange();

            string openingTag = $"<{searchString}";
            string closingTag = $"</{searchString}>";

            foreach (var line in lstConfig)
            {
                if (line.ToLowerInvariant().Trim().StartsWith(openingTag))//startswith is better bet since comment lines could have the tag
                {
                    if (intLineTrackerIndex > 0 && lineRange.StartLineIndex == 0) lineRange.StartLineIndex = intLineTrackerIndex; //since there could be multiple pipelines tags get the first one
                    lineRange.TagString = searchString;
                }

                if (lastTagOccurence == 1)
                {
                    //tags like pipelines are nested within group tag but all of it is part of one main <pipeline> block
                    if (line.ToLowerInvariant().Trim().EndsWith(closingTag))//better bet since comment lines could have the tag
                    {
                        if (intLineTrackerIndex > lineRange.StartLineIndex && intLineTrackerIndex > lineRange.EndLineIndex) lineRange.EndLineIndex = intLineTrackerIndex;
                    }
                }
                else
                {
                    //tags like </events> could be nested within processor so, in such cases, better pick the first one
                    if (line.ToLowerInvariant().Trim().EndsWith(closingTag))//better bet since comment lines could have the tag
                    {
                        if (intLineTrackerIndex > lineRange.StartLineIndex && lineRange.EndLineIndex ==0) lineRange.EndLineIndex = intLineTrackerIndex;
                    }
                }

                intLineTrackerIndex += 1;
            }
        }
    }
}
