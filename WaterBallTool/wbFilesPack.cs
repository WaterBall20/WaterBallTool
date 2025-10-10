using Newtonsoft.Json;
using System.CommandLine;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static WaterBallTool.FileFinder;
using static WaterBallTool.Program;

namespace WaterBallTool
{
    internal class WBFilesPack
    {
        //WBFilesPack(11) | 0.0(2)
        private static readonly byte[] FileNameBytes = System.Text.Encoding.UTF8.GetBytes("WBFilesPack");
        private static readonly byte[] FileVersion = [0, 0];
        private static readonly byte[] FileHeader = new byte[12 + 2 + 8];
        //数据缓存大小
        const int dataBytesLength = 1024 * 1024 * 2;
        public static Command GetCommand()
        {
            //命令
            var wbfpCommand = new Command("wbfp", "水球文件包操作器");
            // wbfpCommand.AddAlias("wbFilesPack");
            wbfpCommand.AddCommand(MergeCommand());//合并
            wbfpCommand.AddCommand(SeparationCommand());//分离
            wbfpCommand.AddCommand(ToolCommand());//工具
            //执行
            return wbfpCommand;
        }

        //合成
        static Command MergeCommand()
        {
            bool pastErro = false;
            //参数
            var inPathArgument = new Argument<string>(name: "文件夹路径", description: "合并的文件夹");
            var outFileArgument = new Argument<string>(name: "包路径", description: "保存水球文件包的路径，无需加后缀，后缀是wbfilespack");
            var PackNameArgument = new Argument<string>(name: "包名称", description: "包名称标记", getDefaultValue: () => "");
            var onHashOption = new Option<bool>(name: "--oh", description: "计算哈希（SHA-256），可选", getDefaultValue: () => false);
            var onSListFileOption = new Option<bool>(name: "--os", description: "列表数据分离", getDefaultValue: () => true);
            var onWriteOptimizationOption = new Option<bool>(name: "--ow", description: "写入优化", getDefaultValue: () => false);
            //命令
            var mCommand = new Command("m", "合成")
                    {
                        inPathArgument,
                        outFileArgument,
                        PackNameArgument,
                        onHashOption,
                        onSListFileOption,
                        onWriteOptimizationOption
                    };
            //mCommand.AddAlias("merge");
            mCommand.SetHandler((inPath, outFile, onHash, packName, onSListFile, onWriteOptimization) =>
            {
                //文件列表
                MWriteLine("搜索文件");
                WBFilesList filesList = new();
                filesList.SearchFiles(inPath, true, onHash);

                if (filesList.pastErro) pastErro = true;
                if (pastErro)
                {
                    Console.WriteLine("刚刚引发了问题，是否继续？y：继续");
                    var readStr = Console.ReadLine();
                    if (readStr != null)
                        if (!(readStr.Equals("y") || readStr.Equals("Y"))) return;

                }
                MergeFilesPack(outFile, filesList, onHash, packName, onSListFile, onWriteOptimization);

            }, inPathArgument, outFileArgument, onHashOption, PackNameArgument, onSListFileOption, onWriteOptimizationOption);
            return mCommand;
        }
        //包文件路径，文件列表，启用哈希，包名称，启用列表数据分离，写入优化
        private static void MergeFilesPack(string outFile, WBFilesList filesList, bool onHash, string packName, bool onSListFile, bool onWriteOptimization)
        {
            try
            {
                //包文件流
                string outFileName = outFile.EndsWith(".wbfilespack")? outFile : $"{outFile}.wbfilespack";

                //文件存在且开启写入优化
                if (File.Exists(outFileName) && onWriteOptimization)
                {
                    (FileStream? fileStream, WBFilesPackData_Read? filesPackData, long jsonDataLength, _) = ReadPackData(outFile);
                    MWriteLine("包文件存在且开启写入优化，将打开文件");
                    
                    MWriteLine("开发未完成，无法使用");
                    fileStream?.Flush();
                    fileStream?.Close();
                    fileStream?.Dispose();
                }
                else
                {
                    using FileStream fileStream = new(outFileName, FileMode.Create, FileAccess.ReadWrite);
                    MWriteLine("创建包文件并打开");
                    //包列表文件流
                    using FileStream listFileStream = new($"{outFileName}.json", FileMode.Create, FileAccess.ReadWrite);
                    MWriteLine("创建包列表文件(json)并打开");
                    WBFilesPackData filesPackData = new();
                    filesPackData.Attribute.Name = packName;
                    filesPackData.Attribute.OnHash = onHash;
                    filesPackData.Attribute.SListFile = onSListFile;
                    if (!onSListFile)
                        filesPackData.FilesList = filesList;
                    //json数据
                    byte[]? jsonDataByte = filesPackData.ToJsonBytes();
                    MWriteLine("json数据处理");
                    //文件数据起始位置
                    long DataStartPosition = FileHeader.Length + jsonDataByte.Length;
                    //循环确定文件数据起始位置
                    while (filesPackData.Attribute.FileDataStartPosition != DataStartPosition)
                    {
                        filesPackData.Attribute.FileDataStartPosition = DataStartPosition;
                        jsonDataByte = filesPackData.ToJsonBytes();
                        DataStartPosition = FileHeader.Length + jsonDataByte.Length;
                    }

                    //json数据长度
                    byte[] jsonDataLength = BitConverter.GetBytes(jsonDataByte.LongLength);
                    //文件夹头处理
                    FileNameBytes.CopyTo(FileHeader, 0);
                    FileVersion.CopyTo(FileHeader, 12);
                    jsonDataLength.CopyTo(FileHeader, 14);
                    fileStream.Write(FileHeader);
                    MWriteLine($"写入文件头，长度：写入：{DataLengthToString(FileHeader.Length)}  总：{DataLengthToString(fileStream.Length)}");
                    //写入json数据
                    fileStream.Write(jsonDataByte);
                    MWriteLine($"写入json数据，长度：写入：{DataLengthToString(jsonDataByte.Length)}  总：{DataLengthToString(fileStream.Length)}");

                    if (onSListFile)
                    {
                        filesPackData.FilesList = filesList;
                        //写入json文件
                        if (onHash)
                        {
                            MWriteLine("json文件将在计算完哈希后写出。");
                        }
                        else
                        {
                            MWriteLine($"写入json文件，长度：写入：{DataLengthToString(filesPackData.WriterToFile(listFileStream, onSListFile ? Formatting.Indented : Formatting.None))}");
                            MWriteLine("json文件已关闭");
                        }
                    }
                    //释放数组
                    jsonDataByte = [];
                    fileStream.Flush();
                    MWriteLine("开始写入数据");

                    //数据缓存
                    byte[] dataBytes = new byte[dataBytesLength];

                    //写入优化专用
                    //数据缓存2，包数据
                    byte[] dataBytes2 = new byte[1024 * 32];
                    //数据缓存3，目标文件数据
                    byte[] dataBytes3 = new byte[dataBytes2.Length];

                    //当前已处理文件数
                    int inFilesIndex = 0;
                    MFiles(filesList.FilesList, "");
                    void MFiles(List<WBFileInfo> filesList2, string relativePath)
                    {
                        foreach (var item in filesList2)
                        {
                            string mRelativePath = $"{relativePath}\\{item.Name}";
                            //如果是文件夹
                            if (item.IsD)
                            {
                                //递归
                                MFiles(((WBFilesInfo)item).FilesList, mRelativePath);
                            }
                            else
                            {
                                try
                                {
                                    //源文件路径
                                    string inFilePath = $"{filesList.FilesPath}{mRelativePath}";
                                    if (File.Exists(inFilePath))
                                    {
                                        //源文件流
                                        using FileStream inFileStream = new(inFilePath, FileMode.Open, FileAccess.Read);
                                        //当前文件的包文件数据起始位置
                                        long dataStartPosition = DataStartPosition + ((WBFileInfo2)item).DataStartPosition;
                                        //当前文件写入长度
                                        long dataWriteLength = 0;
                                        //设置包文件指针位置
                                        fileStream.Position = dataStartPosition;
                                        //上次更新时间
                                        long lastUpdatedTime = DateTime.UtcNow.Ticks;

                                        //哈希相关
                                        SHA256? sHA256 = SHA256.Create();
                                        if (inFileStream.Length != item.Length)
                                        {
                                            if (onSListFile)
                                            {
                                                //文件大小变化警告
                                                if (inFileStream.Length > item.Length)
                                                {
                                                    MWriteLine($"文件{inFilePath}大小相对与搜索时发生了变化（较大[+{DataLengthToString(inFileStream.Length - item.Length)}]），已更新大小", WriteTy.Warn);
                                                }
                                                else if (inFileStream.Length < item.Length)
                                                {
                                                    MWriteLine($"文件{inFilePath}大小相对与搜索时发生了变化（较小[-{DataLengthToString(item.Length - inFileStream.Length)}]），已更新大小", WriteTy.Warn);
                                                }
                                                item.Length = inFileStream.Length;
                                                DataLength(filesList.FilesList);
                                            }
                                            else
                                            {
                                                //文件大小变化警告
                                                if (inFileStream.Length > item.Length)
                                                {
                                                    MWriteLine($"文件{inFilePath}大小相对与搜索时发生了变化（较大[+{DataLengthToString(inFileStream.Length - item.Length)}]），由于是静态包文件，无法更新大小，将会缺少数据", WriteTy.Warn);
                                                }
                                                else if (inFileStream.Length < item.Length)
                                                {
                                                    MWriteLine($"文件{inFilePath}大小相对与搜索时发生了变化（较小[-{DataLengthToString(item.Length - inFileStream.Length)}]），由于是静态包文件，无法更新大小，将会存在额外的空数据", WriteTy.Warn);
                                                }
                                            }
                                        }
                                        //写入
                                        while (dataWriteLength < item.Length)
                                        {
                                            int ReadLength = dataBytes.Length;
                                            //防止过度读取
                                            if (item.Length - dataWriteLength < dataBytes.Length)
                                            {
                                                ReadLength = (int)(item.Length - dataWriteLength);
                                            }
                                            int ReadLength2 = inFileStream.Read(dataBytes, 0, ReadLength);
                                            if (ReadLength2 == -1)
                                            {
                                                MWriteLine($"文件{inFilePath}无法继续读取，因为大小比搜索时的大小小", WriteTy.Warn);
                                                break;
                                            }
                                            fileStream.Write(dataBytes, 0, ReadLength2);
                                            dataWriteLength += ReadLength2;
                                            fileStream.Flush();
                                            //哈希计算
                                            if (onHash) sHA256.TransformBlock(dataBytes, 0, ReadLength, null, 0);
                                            //进度更新
                                            if ((DateTime.UtcNow.Ticks - lastUpdatedTime) > (TimeSpan.TicksPerMillisecond * 10))
                                            {
                                                lastUpdatedTime = DateTime.UtcNow.Ticks;
                                                UpdateProgress(item, mRelativePath);
                                            }
                                        }
                                        //哈希
                                        if (onHash)
                                        {
                                            //结束计算
                                            sHA256.TransformFinalBlock([], 0, 0);
                                            //哈希值
                                            byte[]? fileHash = sHA256.Hash;
                                            if (fileHash != null)
                                            {
                                                //转为文本
                                                ((WBFileInfo2)item).Hash = BitConverter.ToString(fileHash).Replace("-", "").ToLower();
                                            }
                                        }
                                        //计算进度
                                        UpdateProgress(item, mRelativePath);
                                    }
                                    else
                                    {
                                        MWriteLine($"文件 {inFilePath} 不存在,将跳过");
                                    }

                                }
                                catch (UnauthorizedAccessException uae)
                                {
                                    MWriteLine("拒绝访问,将跳过", WriteTy.Warn, uae);
                                }
                                catch (IOException ioe)
                                {
                                    MWriteLine($"I/O错误,将跳过", WriteTy.Warn, ioe);
                                }
                                catch (Exception ex)
                                {
                                    MWriteLine("未知错误,将跳过", WriteTy.Warn, ex);
                                }
                                finally
                                {
                                    inFilesIndex++;
                                }
                            }
                        }
                    }

                    void UpdateProgress(WBFileInfo item, string relativePath)
                    {
                        long PackDataLength = fileStream.Length - (FileHeader.LongLength + jsonDataByte.LongLength);
                        double Progress = (double)PackDataLength * 100 / (double)filesList.DataLength;
                        MWriteLine($"数据：{DataLengthToString(PackDataLength)}/{DataLengthToString(filesList.DataLength)}|文件({inFilesIndex}/{filesList.FilesCount})：{relativePath}", Ty: WriteTy.Progress, Progress: Math.Round(Progress, 2));
                    }

                    void DataLength(List<WBFileInfo> WBFilesList)
                    {
                        long dataPosition = 0;
                        DataLengths(WBFilesList);
                        void DataLengths(List<WBFileInfo> wBFilesList)
                        {
                            foreach (var wbFileInfo in wBFilesList)
                            {
                                //文件
                                if (!wbFileInfo.IsD)
                                {
                                    var wbFileInfo2 = (WBFileInfo2)wbFileInfo;

                                    //算出文件的數據起始位置
                                    wbFileInfo2.DataStartPosition = dataPosition;
                                    dataPosition += wbFileInfo.Length;
                                }
                                else
                                {
                                    //算出文件的數據起始位置
                                    DataLengths(((WBFilesInfo)wbFileInfo).FilesList);
                                }
                            }
                        }
                        filesList.DataLength = dataPosition;
                    }
                    //如果计算哈希则要刷新json数据
                    if (onHash)
                    {
                        
                        if (!onSListFile)
                        {
                            jsonDataByte = filesPackData.ToJsonBytes(onSListFile ? Formatting.Indented : Formatting.None);
                            MWriteLine($"由于开启哈希计算，所以现在进行json数据刷新:{DataLengthToString(jsonDataByte.LongLength)}");
                            //设置包文件指针位置
                            fileStream.Position = FileHeader.LongLength;
                            fileStream.Write(jsonDataByte);
                            fileStream.Flush();
                        }
                        //json文件
                        MWriteLine($"写入json文件，长度：写入：{DataLengthToString(filesPackData.WriterToFile(listFileStream, onSListFile ? Formatting.Indented : Formatting.None))}");
                        MWriteLine("json文件已关闭");
                    }
                    fileStream.Flush();
                    fileStream.Close();
                    MWriteLine("完成，包文件已关闭");
                }
            }
            catch (UnauthorizedAccessException uae)
            {
                MWriteLine("无法创建包文件，拒绝访问", WriteTy.Error, uae);
            }
            catch (SecurityException se)
            {
                MWriteLine($"无法创建包文件，没有足够的权限", WriteTy.Error, se);
            }
            catch (IOException ioe)
            {
                MWriteLine($"I/O错误", WriteTy.Error, ioe);
            }
            catch (Exception ex)
            {
                MWriteLine("未知错误", WriteTy.Warn, ex);
            }
        }

        static void MWriteLine(string info = "", WriteTy Ty = WriteTy.Info, Exception? ex = null, double Progress = 0)
        {
            Program.WriteLine($"[水球文件包操作器][合并]{info}", Ty, ex, Progress);
        }

        //读取包文件数据
        static (FileStream?, WBFilesPackData_Read?, long jsonDataLength, byte[]? fileVersion) ReadPackData(string inFile)
        {
            string inFilePath = inFile.EndsWith(".wbfilespack") ? inFile : ($"{inFile}.wbfilespack");
            if (File.Exists(inFilePath))
            {
                //包文件流
                FileStream fileStream = new(inFilePath, FileMode.Open, FileAccess.ReadWrite);
                PRWriteLine("打开包文件");
                //文件头数据
                byte[] fileHeader = new byte[FileHeader.Length];
                if (fileStream.Read(fileHeader) != FileHeader.Length)
                {
                    PRWriteLine("文件核心数据不完整，无法继续", WriteTy.Error);
                    return (null, null, 0, null);
                }
                PRWriteLine("读取文件头并判断");
                //判断
                //文件名称字节数组
                byte[] fileNameBytes = new byte[FileNameBytes.Length];
                for (int i = 0; i < fileNameBytes.Length; i++)
                {
                    fileNameBytes[i] = fileHeader[i];
                }
                //如果不一致
                if (!BetyArrEquals(fileNameBytes, FileNameBytes))
                {
                    PRWriteLine("文件类型不是水球文件包或不兼容", WriteTy.Error);
                    return (null, null, 0, null);
                }
                //文件版本数组
                byte[] fileVersion = new byte[FileVersion.Length];
                for (int i = 0; i < 2; i++)
                {
                    fileVersion[i] = fileHeader[12 + i];
                }
                //如果不一致
                if (!BetyArrEquals(fileVersion, FileVersion))
                {
                    PRWriteLine("文件格式版本不一致，无法兼容", WriteTy.Error);
                    return (null, null, 0, null);
                }
                //json文件数据长度字节数组
                byte[] jsonDataLengthByte = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    jsonDataLengthByte[i] = fileHeader[12 + 2 + i];
                }
                //json文件数据长度
                long jsonDataLength = BitConverter.ToInt64(jsonDataLengthByte);
                //json数据
                byte[] jsonDataByte = new byte[jsonDataLength];
                if (fileStream.Read(jsonDataByte) != jsonDataLength)
                {
                    SWriteLine("文件关键数据不完整，无法继续", WriteTy.Error);
                    return (null, null, 0, null);
                }
                //json数据文本
                string jsonDataStr = System.Text.Encoding.UTF8.GetString(jsonDataByte);
                //包数据对象
                WBFilesPackData_Read? filesPackData = JsonConvert.DeserializeObject<WBFilesPackData_Read>(jsonDataStr);
                //
                if (filesPackData != null)
                {

                    //文件列表
                    if (filesPackData.Attribute.SListFile)//分离列表数据
                    {
                        string listFilePath = $"{inFilePath}.json";
                        if (!File.Exists(listFilePath))
                        {
                            PRWriteLine($"此包的列表数据已分离，但找不到列表数据文件，无法继续。分离的列表数据文件文件名必须是文件名（包含文件扩展名），此包文件是[{listFilePath}]", WriteTy.Error);
                        }
                        else
                        {
                            using FileStream listFileStream = new(listFilePath, FileMode.Open, FileAccess.Read);
                            jsonDataByte = new byte[listFileStream.Length];
                            PRWriteLine("列表数据分离，正在打开列表数据json");
                            listFileStream.Read(jsonDataByte);
                            jsonDataStr = System.Text.Encoding.UTF8.GetString(jsonDataByte);
                            filesPackData = JsonConvert.DeserializeObject<WBFilesPackData_Read>(jsonDataStr);
                            if (filesPackData == null)
                            {
                                PRWriteLine($"此包的列表数据已分离，但分离的列表数据文件（{listFilePath}）数据无法正常解析，无法继续", WriteTy.Error);
                                return (null, null, 0, null);
                            }
                        }
                    }
                    //判断是否兼容
                    if (!filesPackData.Attribute.Version.IsCompatibility)
                    {
                        PRWriteLine("json数据版本不兼容", WriteTy.Error);
                        return (null, null, 0, null);
                    }
                    if (filesPackData.Attribute.Version.CompatibilityState != 0)
                    {
                        PRWriteLine($"json数据{filesPackData.Attribute.Version.CompatibilityInfo}，可能会存在部分功能不可用。", WriteTy.Warn);
                    }
                    WBFilesList_Read? filesList = filesPackData.FilesList;
                    if (filesList != null)
                    {
                        //判断文件大小是否一致
                        if ((fileStream.Length - FileHeader.Length - jsonDataLength) != filesList.DataLength) PRWriteLine("包文件数据大小不一致，可能不完整或被修改", WriteTy.Warn);
                        PRWriteLine("json数据解析完成");
                        return (fileStream, filesPackData, jsonDataLength, fileVersion);
                    }
                }
            }
            else
            {
                PRWriteLine("包路径无效，请重新输入");
            }
            void PRWriteLine(string info = "", WriteTy Ty = WriteTy.Info, Exception? ex = null, double Progress = 0)
            {
                Program.WriteLine($"[水球文件包操作器][公共方法][读取包文件数据]{info}", Ty, ex, Progress);
            }
            return (null, null, 0, null);
        }

                    //分离
        static Command SeparationCommand()
        {
            //参数
            var inFileArgument = new Argument<string>(name: "包路径", description: "水球文件包（wbfilespack）文件路径");
            var outPathArgument = new Argument<string>(name: "文件夹路径", description: "包分离的保存的文件夹路径");
            var onHashOption = new Option<bool>(name: "--oh", description: "哈希验证", getDefaultValue: () => false);
            var onOverwriteFileOption = new Option<bool>(name: "--of", description: "覆盖文件", getDefaultValue: () => false);
            var onWriteOptimizationOption = new Option<bool>(name: "--ow", description: "写入优化", getDefaultValue: () => true);
            //命令
            var sCommand = new Command("s", "分离")
                    {
                        inFileArgument,
                        outPathArgument,
                        onHashOption,
                        onOverwriteFileOption,
                        onWriteOptimizationOption
                    };
            //mCommand.AddAlias("merge");
            sCommand.SetHandler((inFile, outPath, onHash, onOverwriteFile, onWriteOptimization) =>
            {
                try
                {
                    (FileStream? fileStream, WBFilesPackData_Read? filesPackData, _,_) = ReadPackData(inFile);
                    if ((fileStream != null) && (filesPackData != null))
                    {
                        //如开启哈希验证但不能进行哈希计算
                        if (onHash && !filesPackData.Attribute.OnHash)
                        {
                            SWriteLine("你开启了哈希验证，但该包文件未存储哈希值，无法进行哈希计算，所以将自动关闭哈希验证。", WriteTy.Warn);
                            //关闭哈希验证
                            onHash = false;
                        }
                        WBFilesList_Read? filesList = filesPackData.FilesList;
                        if (filesList != null)
                        {
                            SWriteLine("开始分离");
                            //文件处理
                            //数据缓存
                            byte[] dataBytes = new byte[dataBytesLength];

                            //写入优化专用
                            //数据缓存2，包数据
                            byte[] dataBytes2 = new byte[1024 * 32];
                            //数据缓存3，目标文件数据
                            byte[] dataBytes3 = new byte[dataBytes2.Length];

                            //当前处理文件数
                            int outFilesIndex = 0;
                            //跳过所有文件
                            bool isSkipFillFiles = false;
                            //重命名所有冲突文件
                            bool isRenameFillFiles = false;
                            SFiles(filesList.FilesList, "");
                            //文件列表，跳过子文件，覆盖子文件，重命名子文件
                            void SFiles(List<WBFileInfo_Read> filesList2, string relativePath, WBFileInfo_Read? sWBFileInfo = null, bool isSkipSubfiles = false, bool isOverwriteSubfiles = false, bool isRenameSubfolders = false)
                            {
                                bool isSkipFiles = false;//跳过当前目录子文件
                                bool isOverwriteFiles = false;//覆盖当前目录子文件
                                bool isRenameFiles = false;//重命名当前目录子文件
                                foreach (var item in filesList2)
                                {
                                    string mRelativePath = $"{relativePath}\\{item.Name}";
                                    //如果是文件夹
                                    if (item.IsD)
                                    {
                                        if (File.Exists($"{outPath}{mRelativePath}"))
                                        {
                                            SWriteLine($"文件夹[{item.Name}]所在的路径[{outPath}{mRelativePath}]存在同名文件，无法写入", WriteTy.Warn);
                                        }
                                        else
                                            SFiles(item.FilesList, $"{mRelativePath}\\{item.Name}", item, isSkipSubfiles, isOverwriteSubfiles, isRenameSubfolders);
                                    }
                                    else
                                    {
                                        try
                                        {
                                            SUpdateProgress(mRelativePath);
                                            //跳过
                                            bool isSkip = false;
                                            //重命名
                                            bool isRename = false;
                                            //保存文件路径
                                            string outFilePath = $"{outPath}{mRelativePath}";
                                            //路径
                                            string[] outFilePathArr = outFilePath.Split("\\");
                                            //所在目录路径
                                            string outPath2 = "";
                                            //处理路径
                                            for (int i = 0; i < outFilePathArr.Length - 1; i++)
                                            {
                                                outPath2 += outFilePathArr[i] + "\\";
                                            }
                                            Directory.CreateDirectory(outPath2);
                                            //设置包文件指针位置
                                            fileStream.Position = filesPackData.Attribute.FileDataStartPosition + item.DataStartPosition;
                                            //判断是否为文件夹
                                            if (Directory.Exists(outFilePath))
                                            {
                                                SWriteLine($"文件[{item.Name}]所在的路径[{outFilePath}]存在同名文件夹，无法写入", WriteTy.Warn);
                                            }
                                            else
                                            {
                                                //判断文件是否存在
                                                if (File.Exists(outFilePath))
                                                {
                                                    //覆盖相关
                                                    //|全局，所有子文件，当前目录|
                                                    if (!((isSkipFillFiles || isSkipSubfiles || isSkip) || (onOverwriteFile || isOverwriteSubfiles || isOverwriteFiles) || (isRenameFillFiles || isRenameSubfolders || isRenameFiles)))
                                                    {
                                                        //询问
                                                        bool operate = true;//进行操作
                                                        while (operate)
                                                        {

                                                            SWriteLine($"\n是否覆盖文件\"{outFilePath}\"？请输入2位数\n1位：0.跳过；1.覆盖；2.重命名为\"……(2)\"\n2位：0.仅当前文件；1：以及当前目录之后处理的子文件；2：以及当前目录之后处理的所有子文件；3：以及所有之后处理的文件", WriteTy.Read);
                                                            var readStr = Console.ReadLine();
                                                            if (readStr != null)
                                                            {
                                                                if (readStr.Length == 2)
                                                                {
                                                                    int model = int.Parse(readStr[0].ToString());
                                                                    int model2 = int.Parse(readStr[1].ToString());
                                                                    switch (model)
                                                                    {
                                                                        case 0://跳过
                                                                            switch (model2)
                                                                            {
                                                                                case 0://仅当前文件
                                                                                    isSkip = true;
                                                                                    operate = false;
                                                                                    break;
                                                                                case 1://以及当前目录之后处理的子文件
                                                                                    isSkipFiles = true;
                                                                                    operate = false;
                                                                                    break;
                                                                                case 2://以及当前目录之后处理的所有子文件
                                                                                    isSkipSubfiles = true;
                                                                                    operate = false;
                                                                                    break;
                                                                                case 3://以及所有之后处理的文件
                                                                                    isSkipFillFiles = true;
                                                                                    operate = false;
                                                                                    break;
                                                                                default:
                                                                                    break;
                                                                            }
                                                                            break;
                                                                        case 1://覆盖
                                                                            switch (model2)
                                                                            {
                                                                                case 0://仅当前文件
                                                                                    operate = false;
                                                                                    break;
                                                                                case 1://以及当前目录之后处理的子文件
                                                                                    isOverwriteFiles = true;
                                                                                    operate = false;
                                                                                    break;
                                                                                case 2://以及当前目录之后处理的所有子文件
                                                                                    isOverwriteSubfiles = true;
                                                                                    operate = false;
                                                                                    break;
                                                                                case 3://以及所有之后处理的文件
                                                                                    onOverwriteFile = true;
                                                                                    operate = false;
                                                                                    break;
                                                                                default:
                                                                                    break;
                                                                            }
                                                                            break;
                                                                        case 2://重命名
                                                                            switch (model2)
                                                                            {
                                                                                case 0://仅当前文件
                                                                                    isRename = true;
                                                                                    operate = false;
                                                                                    break;
                                                                                case 1://以及当前目录之后处理的子文件
                                                                                    isRenameFiles = true;
                                                                                    operate = false;
                                                                                    break;
                                                                                case 2://以及当前目录之后处理的所有子文件
                                                                                    isRenameSubfolders = true;
                                                                                    operate = false;
                                                                                    break;
                                                                                case 3://以及所有之后处理的文件
                                                                                    isRenameFillFiles = true;
                                                                                    operate = false;
                                                                                    break;
                                                                                default:
                                                                                    break;
                                                                            }
                                                                            break;
                                                                        default:
                                                                            break;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                    //重命名
                                                    if (isRename || isRenameFiles || isRenameSubfolders || isRenameFillFiles)
                                                    {
                                                        //所在目录处理
                                                        outFilePath = $"{outPath}";
                                                        if (sWBFileInfo != null)
                                                        {
                                                            outFilePath += $"\\{mRelativePath}";
                                                        }
                                                        //文件名处理，前缀加(2)。
                                                        outFilePath += $"\\{Path.GetFileNameWithoutExtension(item.Name)}(2){Path.GetExtension(item.Name)}";

                                                        WriteNew();
                                                    }
                                                    else
                                                    {
                                                        //写入优化：判断是否进行写入优化，暂时替换写出方法
                                                        if (!(isSkip || isSkipFiles || isSkipSubfiles || isSkipFillFiles))
                                                        {
                                                            if (onWriteOptimization)
                                                            {
                                                                //输出文件流
                                                                using FileStream outFileStream = new(outFilePath, FileMode.Open, FileAccess.ReadWrite);

                                                                //当前文件写入长度
                                                                long dataWriteLength = 0;

                                                                //上次更新时间
                                                                long lastUpdatedTime = DateTime.UtcNow.Ticks;

                                                                //哈希相关
                                                                SHA256 sHA256 = SHA256.Create();
                                                                while (dataWriteLength < item.Length)
                                                                {
                                                                    //应读取长度
                                                                    int ReadLength = dataBytes2.Length;
                                                                    //防止过度读取
                                                                    if (item.Length - dataWriteLength < dataBytes2.Length)
                                                                    {
                                                                        ReadLength = (int)(item.Length - dataWriteLength);
                                                                    }
                                                                    //读取包数据
                                                                    int ReadLength2 = fileStream.Read(dataBytes2, 0, ReadLength);
                                                                    //读取原始文件数据
                                                                    int ReadLength3 = outFileStream.Read(dataBytes3, 0, ReadLength);
                                                                    //判断是否读取到头

                                                                    if (ReadLength2 <= 0)
                                                                    {
                                                                        SWriteLine("包文件数据不完整，无法继续", WriteTy.Error);
                                                                        return;
                                                                    }
                                                                    if (ReadLength3 < ReadLength2)
                                                                    {
                                                                        //目标文件大小小于包文件，直接写入
                                                                        //设置输出流位置
                                                                        outFileStream.Position = dataWriteLength;
                                                                        //写出
                                                                        outFileStream.Write(dataBytes, 0, ReadLength2);
                                                                        dataWriteLength += ReadLength2;
                                                                        outFileStream.Flush();
                                                                        //哈希计算
                                                                        if (onHash) sHA256.TransformBlock(dataBytes, 0, ReadLength2, null, 0);
                                                                    }
                                                                    else
                                                                    {
                                                                        //内容判断，使用哈希判断是否一致
                                                                        //计算包数据该段哈希
                                                                        byte[] sHA1_1_h = SHA1.HashData(dataBytes2.AsSpan(0, ReadLength2));
                                                                        //计算目标文件数据该段哈希
                                                                        byte[] sHA1_2_h = SHA1.HashData(dataBytes3.AsSpan(0, ReadLength2));
                                                                        if (!BetyArrEquals(sHA1_1_h, sHA1_2_h))
                                                                        {
                                                                            //设置输出流位置
                                                                            outFileStream.Position = dataWriteLength;
                                                                            //写出
                                                                            outFileStream.Write(dataBytes2, 0, ReadLength2);
                                                                            outFileStream.Flush();
                                                                        }
                                                                        dataWriteLength += ReadLength2;
                                                                        //哈希计算
                                                                        if (onHash) sHA256.TransformBlock(dataBytes2, 0, ReadLength2, null, 0);
                                                                    }

                                                                    //进度更新
                                                                    if ((DateTime.UtcNow.Ticks - lastUpdatedTime) > (TimeSpan.TicksPerMillisecond * 10))
                                                                    {
                                                                        lastUpdatedTime = DateTime.UtcNow.Ticks;
                                                                        SUpdateProgress(mRelativePath);
                                                                    }
                                                                }
                                                                //设置文件大小
                                                                outFileStream.SetLength(item.Length);
                                                            }
                                                            else
                                                                WriteNew();
                                                        }
                                                    }
                                                }

                                                else
                                                {

                                                    WriteNew();
                                                }
                                                void WriteNew()
                                                {
                                                    //输出文件流
                                                    using FileStream outFileStream = new(outFilePath, FileMode.Create, FileAccess.Write);

                                                    //当前文件写入长度
                                                    long dataWriteLength = 0;

                                                    //上次更新时间
                                                    long lastUpdatedTime = DateTime.UtcNow.Ticks;

                                                    //哈希相关
                                                    SHA256 sHA256 = SHA256.Create();

                                                    //写入
                                                    while (dataWriteLength < item.Length)
                                                    {
                                                        int ReadLength = dataBytes.Length;
                                                        //防止过度读取
                                                        if (item.Length - dataWriteLength < dataBytes.Length)
                                                        {
                                                            ReadLength = (int)(item.Length - dataWriteLength);
                                                        }
                                                        int ReadLength2 = fileStream.Read(dataBytes, 0, ReadLength);
                                                        //判断是否读取到头

                                                        if (ReadLength2 <= 0)
                                                        {
                                                            SWriteLine("包文件数据不完整，无法继续", WriteTy.Error);
                                                            return;
                                                        }
                                                        outFileStream.Write(dataBytes, 0, ReadLength2);
                                                        dataWriteLength += ReadLength2;
                                                        outFileStream.Flush();
                                                        //哈希计算
                                                        if (onHash) sHA256.TransformBlock(dataBytes, 0, ReadLength2, null, 0);

                                                        //进度更新
                                                        if ((DateTime.UtcNow.Ticks - lastUpdatedTime) > (TimeSpan.TicksPerMillisecond * 10))
                                                        {
                                                            lastUpdatedTime = DateTime.UtcNow.Ticks;
                                                            SUpdateProgress(mRelativePath);
                                                        }
                                                    }
                                                    //进度更新
                                                    if ((DateTime.UtcNow.Ticks - lastUpdatedTime) > (TimeSpan.TicksPerMillisecond * 10))
                                                    {
                                                        lastUpdatedTime = DateTime.UtcNow.Ticks;
                                                        SUpdateProgress(mRelativePath);
                                                    }
                                                    //哈希
                                                    if (onHash)
                                                    {
                                                        //结束计算
                                                        sHA256.TransformFinalBlock([], 0, 0);
                                                        //哈希值
                                                        byte[]? fileHash = sHA256.Hash;
                                                        string fileHashStr = "";
                                                        if (fileHash != null)
                                                        {
                                                            //转为文本
                                                            fileHashStr = BitConverter.ToString(fileHash).Replace("-", "").ToLower();
                                                        }
                                                        //验证
                                                        if (fileHashStr.Equals(item.Hash)) {  /*SWriteLine($"[哈希验证][通过]{outFilePath}");*/ } else { SWriteLine($"[哈希验证][不一致]{outFilePath}", WriteTy.Warn); }
                                                    }
                                                }
                                            }
                                            //计算进度
                                            SUpdateProgress(mRelativePath);
                                            void SUpdateProgress(string relativePath)
                                            {
                                                (string progress_str, double progress) = UpdateProgress(fileStream.Position, filesPackData.Attribute.FileDataStartPosition, filesList.DataLength, outFilesIndex, filesList.FilesCount, relativePath);
                                                SWriteLine(progress_str, WriteTy.Progress, Progress: progress);
                                            }
                                        }
                                        catch (UnauthorizedAccessException uae)
                                        {
                                            SWriteLine("拒绝访问,将跳过", WriteTy.Warn, uae);
                                        }
                                        catch (IOException ioe)
                                        {
                                            SWriteLine($"I/O错误,将跳过", WriteTy.Warn, ioe);
                                        }
                                        catch (Exception ex)
                                        {
                                            SWriteLine("未知错误,将跳过", WriteTy.Warn, ex);
                                        }
                                        finally
                                        {
                                            outFilesIndex++;
                                        }
                                    }
                                }
                            }
                            fileStream.Close();
                            fileStream.Dispose();
                            SWriteLine("完成");
                            
                        }
                    }
                }
                catch (UnauthorizedAccessException uae)
                {
                    SWriteLine("拒绝访问", WriteTy.Warn, uae);
                }
                catch (IOException ioe)
                {
                    SWriteLine($"I/O错误", WriteTy.Error, ioe);
                }
                catch (Exception ex)
                {
                    SWriteLine("未知错误", WriteTy.Warn, ex);
                }
            }, inFileArgument, outPathArgument, onHashOption, onOverwriteFileOption, onWriteOptimizationOption);
            return sCommand;
        }
        static bool BetyArrEquals(byte[] array1, byte[] array2)
        {
            if (array1.Length == array2.Length)
            {
                for (int i = 0; i < array1.Length; i++)
                {
                    if (array1[i] != array2[i]) return false;
                }
            }
            else return false;
            return true;
        }
        static void SWriteLine(string info = "", WriteTy Ty = WriteTy.Info, Exception? ex = null, double Progress = 0)
        {
            Program.WriteLine($"[水球文件包操作器][分离]{info}", Ty, ex, Progress);
        }

        //包文件位置,数据起始位置,数据大小
        static (string, double) UpdateProgress(long fileStreamPosition, long FileDataStartPosition, long DataLength, int outFilesIndex, int FilesCount, string RelativePath)
        {
            //包数据位置 = 包文件位置 - 数据起始位置
            long PackDataPosition = fileStreamPosition - FileDataStartPosition;
            //进度 = 包数据位置 / 包数据大小
            double Progress = (double)PackDataPosition * 100 / (double)DataLength;
            //(文本：包数据位置字节/包数据大小字节 |文件(输出文件索引/文件数)：当前写出文件的相对路径，进度)
            return ($"数据：{DataLengthToString(PackDataPosition)}/{DataLengthToString(DataLength)}|文件({outFilesIndex}/{FilesCount})：{RelativePath}", Math.Round(Progress, 2));
        }

        static Command ToolCommand()
        {
            var tCommand = new Command("t", "工具");
            tCommand.AddCommand(HashVerificationCommand());
            tCommand.AddCommand(FilesPackInfoCommand());

            //哈希验证
            static Command HashVerificationCommand()
            {
                var inFilePath = new Argument<string>("包路径", "水球文件包(wbfilespack)路径");
                var tHVCommand = new Command("hv", "哈希验证")
                {
                    inFilePath
                };

                tHVCommand.SetHandler((inFile) =>
                {
                    try
                    {
                        (FileStream? fileStream, WBFilesPackData_Read? filesPackData, long jsonDataLength, _) = ReadPackData(inFile);
                        if (fileStream != null && filesPackData != null)
                        {
                            if (filesPackData != null)
                            {
                                HashVerification(filesPackData, fileStream, jsonDataLength);
                                fileStream.Close();
                                fileStream.Dispose();
                                THVWriteLine("完成");
                                
                            }
                        }
                    }
                    catch (UnauthorizedAccessException uae)
                    {
                        THVWriteLine("拒绝访问", WriteTy.Warn, uae);
                    }
                    catch (IOException ioe)
                    {
                        THVWriteLine($"I/O错误", WriteTy.Error, ioe);
                    }
                    catch (Exception ex)
                    {
                        THVWriteLine("未知错误", WriteTy.Warn, ex);
                    }
                }, inFilePath);
                return tHVCommand;
            }
            static void HashVerification(WBFilesPackData_Read filesPackData, FileStream fileStream, long jsonDataLength)
            {
                //如文件未存储哈希
                if (!filesPackData.Attribute.OnHash)
                {
                    THVWriteLine("该包文件未存储哈希值，无法进行哈希验证。", WriteTy.Error);
                    return;
                }
                //文件列表
                WBFilesList_Read? filesList = filesPackData.FilesList;
                if (filesList != null)
                {THVWriteLine("开始哈希验证");
                    //文件处理
                    //数据缓存
                    byte[] dataBytes = new byte[dataBytesLength];
                    int inFilesindex = 0;
                    HVFiles(filesList.FilesList, "");
                    void HVFiles(List<WBFileInfo_Read> filesList2, string relativePath)
                    {
                        foreach (var item in filesList2)
                        {
                            string  mRelativePath = $"{relativePath}\\{item.Name}";
                            //如果是文件夹
                            if (item.IsD)
                            {
                                HVFiles(item.FilesList, mRelativePath);
                            }
                            else
                            {
                                try
                                {
                                    //判断是否保存哈希值
                                    if (item.Hash.Equals(new string(' ', 64)))
                                    {
                                        THVWriteLine($"文件{mRelativePath}未存储哈希值，无法进行哈希验证。", WriteTy.Warn);
                                        break;
                                    }
                                    //当前文件的包文件数据起始位置
                                    long dataStartPosition = filesPackData.Attribute.FileDataStartPosition + item.DataStartPosition;
                                    //当前文件读取长度
                                    long dataReadLength = 0;
                                    //设置包文件指针位置
                                    fileStream.Position = dataStartPosition;
                                    //上次更新时间
                                    long lastUpdatedTime = DateTime.UtcNow.Ticks;

                                    //哈希相关
                                    SHA256 sHA256 = SHA256.Create();

                                    //读取
                                    while (dataReadLength < item.Length)
                                    {
                                        int ReadLength = dataBytes.Length;
                                        //防止过度读取
                                        if (item.Length - dataReadLength < dataBytes.Length)
                                        {
                                            ReadLength = (int)(item.Length - dataReadLength);
                                        }
                                        int ReadLength2 = fileStream.Read(dataBytes, 0, ReadLength);
                                        //判断是否读取到头
                                        if (ReadLength2 <= 0)
                                        {
                                            THVWriteLine("包文件数据不完整，无法继续", WriteTy.Error);
                                            return;
                                        }
                                        dataReadLength += ReadLength2;
                                        //哈希计算
                                        sHA256.TransformBlock(dataBytes, 0, ReadLength2, null, 0);
                                        //进度更新
                                        if ((DateTime.UtcNow.Ticks - lastUpdatedTime) > (TimeSpan.TicksPerMillisecond * 10))
                                        {
                                            lastUpdatedTime = DateTime.UtcNow.Ticks;
                                            UpdateProgress(mRelativePath);
                                        }
                                    }
                                    //结束计算
                                    sHA256.TransformFinalBlock([], 0, 0);
                                    //哈希值
                                    byte[]? fileHash = sHA256.Hash;
                                    string fileHashStr = "";
                                    if (fileHash != null)
                                    {
                                        //转为文本
                                        fileHashStr = BitConverter.ToString(fileHash).Replace("-", "").ToLower();
                                    }
                                    //验证
                                    if (fileHashStr.Equals(item.Hash)) { /*THVWriteLine($"[通过]{item.RelativePath}");*/ } else { THVWriteLine($"[不一致]{mRelativePath}", WriteTy.Warn); }

                                    //计算进度
                                    UpdateProgress(mRelativePath);
                                    void UpdateProgress(string  relativePath)
                                    {
                                        long PackDataPosition = fileStream.Position - (FileHeader.LongLength + jsonDataLength);
                                        double Progress = (double)PackDataPosition * 100 / (double)filesList.DataLength;
                                        THVWriteLine($"数据：{DataLengthToString(PackDataPosition)}/{DataLengthToString(filesList.DataLength)}|文件({inFilesindex}/{filesList.FilesCount})：{relativePath}", Ty: WriteTy.Progress, Progress: Math.Round(Progress, 2));
                                    }
                                    //THVWriteLine($"写入{outFilePath}，长度：读取起始：{dataStartPosition}  写入：{dataWriteLength}");

                                }
                                catch (UnauthorizedAccessException uae)
                                {
                                    THVWriteLine("拒绝访问,将跳过", WriteTy.Warn, uae);
                                }
                                catch (IOException ioe)
                                {
                                    THVWriteLine($"I/O错误,将跳过", WriteTy.Warn, ioe);
                                }
                                catch (Exception ex)
                                {
                                    THVWriteLine("未知错误,将跳过", WriteTy.Warn, ex);
                                }
                                finally
                                {
                                    inFilesindex++;
                                }
                            }
                        }
                    }
                }
            }
            static void THVWriteLine(string info = "", WriteTy Ty = WriteTy.Info, Exception? ex = null, double Progress = 0)
            {
                WriteLine($"[水球文件包操作器][工具][哈希验证]{info}", Ty, ex, Progress);
            }

            static Command FilesPackInfoCommand()
            {
                var inFilePath = new Argument<string>("包路径", "水球包文件(wbfilespack)路径");
                var fpiCommand = new Command("fpi", "显示水球包文件信息")
                {
                    inFilePath
                };

                fpiCommand.SetHandler((inFile) =>
                {
                    try
                    {
                        (FileStream? fileStream, WBFilesPackData_Read? filesPackData, long jsonDataLength, byte[]? fileVersion) = ReadPackData(inFile);
                        if (fileStream != null && filesPackData != null)
                        {
                            if (filesPackData != null)
                            {
                                //文件列表
                                WBFilesList_Read? filesList = filesPackData.FilesList;
                                if (filesList != null)
                                {
                                    //判断文件大小是否一致
                                    if ((fileStream.Length - FileHeader.Length - jsonDataLength) != filesList.DataLength) THVWriteLine("包文件数据大小不一致，可能不完整或被修改");
                                    FPIWriteLine("json数据解析完成");
                                    FPIWriteLine($"文件版本：{BitConverter.ToString((fileVersion == null) ? [255,255] : fileVersion)}", Ty: WriteTy.None);
                                    FPIWriteLine($"json数据版本：{filesPackData.Attribute.Version.Value} 最低兼容版本：{filesPackData.Attribute.Version.Compatible}", Ty: WriteTy.None);
                                    FPIWriteLine($"搜索路径：{filesPackData.FilesList.FilesPath}", Ty: WriteTy.None);
                                    FPIWriteLine($"文件数据起始位置：{filesPackData.Attribute.FileDataStartPosition}", Ty: WriteTy.None);
                                    FPIWriteLine($"文件数据大小:{DataLengthToString(filesPackData.FilesList.DataLength)}({filesPackData.FilesList.DataLength})", Ty: WriteTy.None);
                                    FPIWriteLine($"总文件数（不包括文件夹）:{filesPackData.FilesList.FilesCount}", Ty: WriteTy.None);
                                    FPIWriteLine($"启用哈希计算:{filesPackData.Attribute.OnHash}", WriteTy.None);
                                    FPIWriteLine($"包名称:{filesPackData.Attribute.Name}", WriteTy.None);
                                    FPIWriteLine($"列表数据分离:{filesPackData.Attribute.SListFile}", WriteTy.None);

                                    //操作分配器
                                    WriteLine("接下来？0:关闭;1:哈希验证;2:子文件列表", WriteTy.Read);
                                    while (true)
                                    {
                                        string? Str = Console.ReadLine();
                                        if (Str != null)
                                        {
                                            //关闭
                                            if (Str.Equals("0")) break;
                                            //哈希验证
                                            if (Str.Equals("1"))
                                            {
                                                HashVerification(filesPackData, fileStream, jsonDataLength);
                                                break;
                                            }
                                            //显示列表
                                            if (Str.Equals("2"))
                                            {
                                                FilesList();
                                                break;
                                            }
                                            FPIWriteLine($"请输入有效序号", WriteTy.Warn);
                                        }
                                        else
                                        {
                                            FPIWriteLine($"请输入序号", WriteTy.Warn);
                                        }
                                    }
                                    //HVFiles(filesList.FilesList);
                                    //子文件信息
                                    void FilesList()
                                    {
                                        FilesListInfo(filesList.FilesList, new WBFileInfo_Read() { IsD = true, Name = "\\" }, "");
                                        bool FilesListInfo(List<WBFileInfo_Read> filesList2, WBFileInfo_Read IFileInfo, string relavivePath)
                                        {
                                            while (true)
                                            {
                                                try
                                                {
                                                    //子文件列表
                                                    FPIWriteLine($"子文件[{relavivePath}]:({filesList2.Count}):输入序号可进入文件夹（特殊:-1:返回(在根操作将关闭);-2:关闭程序）\n 序号 | 是文件夹 |      文件大小(字节)      |  子文件数  |   数据起始位置  |{(filesPackData.Attribute.OnHash ? $"{new string(' ', 31)}哈希{new string(' ', 31)}| " : "")} 文件名", Ty: WriteTy.None);
                                                    foreach (var item in filesList2)
                                                    {
                                                        //序号
                                                        string indexStr = $"{filesList2.IndexOf(item)}";
                                                        if (indexStr.Length < 4) indexStr = $"{new string(' ', (4 - indexStr.Length) / 2)}{indexStr}{new string(' ', (4 - indexStr.Length) - (4 - indexStr.Length) / 2)}";
                                                        //大小
                                                        string LengthStr = $"{DataLengthToString(item.Length)}({item.Length})";
                                                        if (LengthStr.Length < 25) LengthStr = $"{new string(' ', (25 - LengthStr.Length) / 2)}{LengthStr}{new string(' ', (25 - LengthStr.Length) - (25 - LengthStr.Length) / 2)}";
                                                        //子文件数
                                                        string FilesCountStr = $"{item.FilesCount}";
                                                        if (FilesCountStr.Length < 10) FilesCountStr = $"{new string(' ', (10 - FilesCountStr.Length) / 2)}{FilesCountStr}{new string(' ', (10 - FilesCountStr.Length) - (10 - FilesCountStr.Length) / 2)}";
                                                        //数据起始位置
                                                        string DataStartPosition = $"{item.DataStartPosition}";
                                                        if (DataStartPosition.Length < 15) DataStartPosition = $"{new string(' ', (15 - DataStartPosition.Length) / 2)}{DataStartPosition}{new string(' ', (15 - DataStartPosition.Length) - (15 - DataStartPosition.Length) / 2)}";
                                                        //               -|      序号     |                      是文件夹                       |   文件大小   |      子文件数      |       数据起始位置      |哈希值|文件名|
                                                        WriteLine($" {indexStr} | {(item.IsD ? "是文件夹" : "非文件夹")} | {LengthStr}| {FilesCountStr} | {DataStartPosition} |{(filesPackData.Attribute.OnHash ? $" {(item.Hash.Equals("") ? new string(' ', 64) : item.Hash)} |" : "")} {item.Name}", WriteTy.None);
                                                    }

                                                    //输入内容
                                                    WriteLine($"序号?", WriteTy.Read);
                                                    string? Str = Console.ReadLine();
                                                    if (Str != null)
                                                    {
                                                        int index = int.Parse(Str);
                                                        if (index == -2) return true;//关闭
                                                        if (index == -1) break;//返回
                                                        else
                                                        {
                                                            if (index < filesList2.Count)
                                                            {
                                                                var item = filesList2[index];
                                                                if (item.IsD)
                                                                {
                                                                    if (item.FilesList.Count != 0)
                                                                    {
                                                                        if (FilesListInfo(item.FilesList, item, $"{relavivePath}\\{item.Name}")) return true;
                                                                    }
                                                                    else
                                                                    {
                                                                        FPIWriteLine($"{item.Name}没有子文件，无法进入", WriteTy.Warn);
                                                                    }

                                                                }
                                                                else
                                                                {
                                                                    FPIWriteLine($"{item.Name}不是文件夹，请重新输入", WriteTy.Warn);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                FPIWriteLine($"序号不在范围，请重新输入", WriteTy.Warn);
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        FPIWriteLine($"请输入序号", WriteTy.Warn);
                                                    }
                                                }
                                                catch
                                                {
                                                    FPIWriteLine($"请输入序号", WriteTy.Warn);
                                                }
                                            }
                                            return false;
                                        }
                                    }
                                    //退出程序
                                    fileStream.Close();
                                    fileStream.Dispose();
                                    
                                }
                            }
                        }
                        
                    }
                    catch (UnauthorizedAccessException uae)
                    {
                        FPIWriteLine("拒绝访问", WriteTy.Warn, uae);
                    }
                    catch (IOException ioe)
                    {
                        FPIWriteLine($"I/O错误", WriteTy.Error, ioe);
                    }
                    catch (Exception ex)
                    {
                        FPIWriteLine("未知错误", WriteTy.Warn, ex);
                    }

                }, inFilePath);

                return fpiCommand;
            }
            static void FPIWriteLine(string info = "", WriteTy Ty = WriteTy.Info, Exception? ex = null, double Progress = 0)
            {
                WriteLine($"[水球文件包操作器][工具][文件信息]{info}", Ty, ex, Progress);
            }


            return tCommand;
        }

        public static string DataLengthToString(long DataLength)
        {
            //基本单位值
            //KB1同时用于进制计算
            const long KB1 = 1024;
            const long MB1 = KB1 * KB1;
            const long GB1 = MB1 * KB1;
            const long TB1 = GB1 * KB1;
            //额外单位值(仅用于判断)
            const long MB10 = MB1 * 10;
            const long MB100 = MB10 * 10;
            const long GB10 = GB1 * 10;
            const long GB100 = GB10 * 10;

            //判断范围
            if (DataLength > TB1) { return $"{Math.Round(((double)DataLength / TB1), 2)}TB"; }
            if (DataLength > GB100) { return $"{Math.Round(((double)DataLength / GB1), 1)}GB"; }
            if (DataLength > GB10) { return $"{Math.Round(((double)DataLength / GB1), 3)}GB"; }
            if (DataLength > GB1) { return $"{Math.Round(((double)DataLength / GB1), 3)}GB"; }
            if (DataLength > MB100) { return $"{Math.Round(((double)DataLength / MB1), 1)}MB"; }
            if (DataLength > MB10) { return $"{Math.Round(((double)DataLength / MB1), 2)}MB"; }
            if (DataLength > MB1) { return $"{Math.Round(((double)DataLength / MB1), 3)}MB"; }
            if (DataLength > KB1) { return $"{Math.Round(((double)DataLength / KB1), 3)}KB"; }
            return $"{DataLength}B";
        }

        //包文件读取专用类
        public class WBFilesPackData_Read
        {
            /*---V0---*/
            //属性
            public Attribute_Read Attribute { get; set; } = new();
            //文件列表
            public WBFilesList_Read FilesList { get; set; } = new();
            /*---V1---*/
            //启用哈希计算(V3迁移到属性)
        }

        public class Attribute_Read
        {
            //文件数据起始位置
            public long FileDataStartPosition { get; set; } = 0;
            //版本数据
            public DataVersion_Read Version { get; set; } = new();
            /*---V3---*/
            //(迁移)启用哈希计算
            public bool OnHash { get; set; } = false;
            /*---V3---*/
            public string Name { get; set; } = "";
            /*---V4---*/
            public bool SListFile { get; set; }
        }

        public class DataVersion_Read
        {
            //文件标记的版本
            public int Value { get; set; } = 0;
            //文件标记的兼容版本
            public int Compatible { get; set; } = 0;

            //判断是否兼容
            public bool IsCompatibility
            {
                get
                {
                    if (CompatibilityState < 0) return false;
                    else return true;
                }
            }
            //兼容状态-1过低，-2过高，1偏低，2偏高，0一致
            public int CompatibilityState
            {
                get
                {
                    //当前解析器版本
                    int Version = 1;
                    //兼容最低版本
                    int VersionCompatible = 1;
                    //判断文件的版本是否过低
                    if (Value < Version) return (Value < VersionCompatible) ? -1 : 1;
                    //判断文件的版本是否过高
                    if (Value > Version) return (Compatible > Version) ? -2 : 2;
                    //都不是
                    return 0;
                }
            }
            //兼容信息
            public string CompatibilityInfo
            {
                get
                {
                    return CompatibilityState switch
                    {
                        0 => "正常",
                        1 => "偏低",
                        2 => "偏高",
                        -1 => "过低",
                        -2 => "过高",
                        _ => "未知",
                    };
                }
            }
        }


        // 定义一个表示文件信息的类
        public class WBFileInfo_Read
        {
            /*===V0===*/
            //名称
            public string Name { get; set; } = "";
            //长度
            public long Length { get; set; } = 0;
            /*===V2===*/
            //是文件夹
            public bool IsD { get; set; } = false;
            //当是文件夹时的子文件集合
            public List<WBFileInfo_Read> FilesList { get; set; } = [];
            //所有子文件数(不包括文件夹)
            public int FilesCount { get; set; } = 0;

            /*====包文件专用====*/
            //数据起始位置
            public long DataStartPosition { get; set; } = 0;
            /*===V1===*/
            //哈希
            public string Hash { get; set; } = "";
            /*===V4===*/
            //数据位置分段，修改文件后用
            public List<long> DataPosition { get; set; } = [];
        }

        // 定义一个表示文件搜索结果列表的类
        public class WBFilesList_Read
        {
            /*---V0---*/
            //文件路径
            public string FilesPath { get; set; } = "";
            //文件列表
            public List<WBFileInfo_Read> FilesList { get; set; } = [];
            //总数据大小
            public long DataLength { get; set; } = 0;
            /*---V2---*/
            //总文件大小
            public int FilesCount
            {
                get
                {
                    int Count = 0;
                    foreach (var item in FilesList)
                    {
                        //如果是文件夹就加子文件数，否则加1
                        Count += item.IsD ? item.FilesCount : 1;
                    }
                    return Count;
                }
            }

        }



        //包文件数据类
        public class WBFilesPackData
        {
            //属性
            public Attribute Attribute { get; set; } = new();
            //文件列表
            public WBFilesList FilesList { get; set; } = new();
            //启用哈希计算(V3迁移到属性)
            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }

            //转换为Json数据数组
            public byte[] ToJsonBytes(Formatting jsonFo = Formatting.None)
            {
                MemoryStream memoryStream = new();
                StreamWriter streamWriter = new(memoryStream);
                JsonSerializer jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings { Formatting = jsonFo });
                jsonSerializer.Serialize(streamWriter, this);
                streamWriter.Flush();
                memoryStream.Flush();
                memoryStream.Position = 0;
                byte[] bytes = new byte[memoryStream.Length];
                memoryStream.Read(bytes);
                return bytes;
            }

            //写入到文件
            public long WriterToFile(FileStream fileStream, Formatting jsonFo = Formatting.None)
            {
                StreamWriter streamWriter = new(fileStream);
                JsonSerializer jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings { Formatting = jsonFo });
                jsonSerializer.Serialize(streamWriter, this);
                streamWriter.Flush();
                fileStream.Flush();
                return fileStream.Length;
            }
        }

        public class Attribute
        {
            //文件数据起始位置
            public long FileDataStartPosition { get; set; } = 0;
            //版本数据
            public DataVersion Version { get; set; } = new();
            //(迁移)启用哈希计算
            public bool OnHash { get; set; } = false;
            //包名称
            public string Name { get; set; } = "";
            //分离列表数据
            public bool SListFile { get; set; }
        }
        public class DataVersion
        {
            //文件标记的版本
            public int Value { get; set; } = 1;
            //文件标记的兼容版本
            public int Compatible { get; set; } = 1;

        }
    }
}
