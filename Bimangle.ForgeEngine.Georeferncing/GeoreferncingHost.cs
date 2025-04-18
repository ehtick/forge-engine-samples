﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Bimangle.ForgeEngine.Common.Georeferenced;
using Bimangle.ForgeEngine.Common.Types;
using Bimangle.ForgeEngine.Common.Utils;
using Bimangle.ForgeEngine.Georeferncing.Interface;
using Bimangle.ForgeEngine.Georeferncing.Utility;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace Bimangle.ForgeEngine.Georeferncing
{
    public class GeoreferncingHost : IGeoreferncingHost, IAdapterHost, IDisposable
    {
        private readonly string _HomeFolder;
        private Adapter _Adapter;
        private IProj _ProjValidator;

        public static GeoreferncingHost Create(Adapter adapter, string homeFolder)
        {
            IProj projValidator = null;
            try
            {
                projValidator = adapter.CreateProj(homeFolder);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }

            return new GeoreferncingHost(homeFolder, adapter, projValidator);
        }

        private GeoreferncingHost(string homeFolder, Adapter adapter, IProj projValidator)
        {
            _HomeFolder = homeFolder;
            _Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _ProjValidator = projValidator;

            _Adapter.Init(this);
        }

        /// <summary>
        /// 预加载
        /// </summary>
        public void Preload()
        {
            if (_ProjValidator == null) return;

            //用单独的线程预加载初始化 ProjValidator
            ThreadPool.QueueUserWorkItem(_ => _ProjValidator?.Init());
        }

        #region IDisposable

        public void Dispose()
        {
            _ProjValidator?.Dispose();
            _ProjValidator = null;

            _Adapter = null;
        }

        #endregion

        #region Implementation of IGeoreferncingHost

        IProj IGeoreferncingHost.GetProjValidator()
        {
            return _ProjValidator;
        }

        public bool CheckProjDefinition(string projDefinition, out string projWkt)
        {
            if (_ProjValidator != null && 
                _ProjValidator.Check(projDefinition, out projWkt))
            {
                return true;
            }

            projWkt = null;
            return false;
        }

        /// <summary>
        /// 从目标文件加载 proj 信息
        /// </summary>
        /// <param name="projFilePath"></param>
        /// <returns></returns>
        public string GetProjDefinition(string projFilePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(projFilePath)) return null;
                if (File.Exists(projFilePath) == false) return null;

                var projContent = File.ReadAllText(projFilePath, Encoding.UTF8);
                if (CheckProjDefinition(projContent, out _))
                {
                    return projContent;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }

            return null;
        }

        public IList<ProjSourceItem> GetProjSourceItems()
        {
            var items = new List<ProjSourceItem>();

            //加入自定义选项
            {
                var label = ProjSourceType.Custom.GetString();
                items.Add(new ProjSourceItem(label, ProjSourceType.Custom, null, null));
            }

            //加入内置坐标系定义选项
            {
                var projDefinition = _Adapter.GetEmbedProjDefinition();
                if (string.IsNullOrWhiteSpace(projDefinition) == false)
                {
                    var label = ProjSourceType.Embed.GetString();
                    items.Add(new ProjSourceItem(label, ProjSourceType.Embed, null, projDefinition));
                }
            }


            //加入项目默认定义
            {
                var projDefinition = GetDefaultProjDefinition();
                if (string.IsNullOrWhiteSpace(projDefinition) == false)
                {
                    var label = ProjSourceType.Default.GetString();
                    items.Add(new ProjSourceItem(label, ProjSourceType.Default, null, projDefinition));
                }
            }

            //加入项目文件夹下 metadata.xml 文件选项
            {
                var sourceType = ProjSourceType.MetadataXml;
                var metadataXml = GetDefaultMetadataXml();
                if (metadataXml != null)
                {
                    items.Add(new ProjSourceItem(MetadataXml.FILE_NAME, sourceType, metadataXml.FilePath, null));
                }
            }

            //加入项目文件夹下 *.proj 文件选项
            {
                var sourceType = ProjSourceType.ProjectFolder;
                var files = GetProjectProjFiles();
                if (files != null && files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        var filePath = file.Key;
                        var projDefinition = file.Value;
                        var fileName = Path.GetFileName(filePath);
                        var label = $@"{sourceType.GetString()}: {fileName}";
                        items.Add(new ProjSourceItem(label, sourceType, filePath, projDefinition));
                    }
                }
            }

            //加入最近使用的 *.prj 文件选项
            {
                var sourceType = ProjSourceType.Recently;
                var files = _Adapter.GetRecentlyProjFiles();
                if (files != null && files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        var filePath = file.Key;
                        var projDefinition = file.Value;
                        var label = $@"{sourceType.GetString()}: {filePath}";
                        items.Add(new ProjSourceItem(label, sourceType, filePath, projDefinition));
                    }
                }
            }

            //加入从文件中加载
            {
                var label = ProjSourceType.Browse.GetString();
                items.Add(new ProjSourceItem(label, ProjSourceType.Browse, null, null));
            }

            //加入创建投影定义
            {
                var label = ProjSourceType.Create.GetString();
                items.Add(new ProjSourceItem(label, ProjSourceType.Create, null, null));
            }

            return items;
        }

        public IList<VerticalGeoidGrid> GetVerticalGeoidGridItems()
        {
            var prjLibPath = Path.Combine(_HomeFolder, @"Common", "Proj");
            return VerticalGeoidGrid.GetList(prjLibPath);
        }

        public IList<GenericItem<ProjOffsetType>> GetProjOffsetTypes()
        {
            return new List<GenericItem<ProjOffsetType>>
            {
                new GenericItem<ProjOffsetType>(ProjOffsetType.None, ProjOffsetType.None.GetString()),
                // new GenericItem<ProjOffsetType>(ProjOffsetType._2D_Params3, ProjOffsetType._2D_Params3.GetString()),
                new GenericItem<ProjOffsetType>(ProjOffsetType._2D_Params4, ProjOffsetType._2D_Params4.GetString()),
                //new GenericItem<ProjOffsetType>(ProjOffsetType._3D_Params3, ProjOffsetType._3D_Params3.GetString()),
                //new GenericItem<ProjOffsetType>(ProjOffsetType._3D_Params4, ProjOffsetType._3D_Params4.GetString()),
                //new GenericItem<ProjOffsetType>(ProjOffsetType._3D_Params7, ProjOffsetType._3D_Params7.GetString()),
            };
        }

        public string GetModelFilePath()
        {
            var filePath = _Adapter.GetFilePath();
            if (string.IsNullOrWhiteSpace(filePath) || 
                File.Exists(filePath) == false)
            {
                return null;
            }
            return filePath;
        }

        public string GetDefaultProjFilePath()
        {
            var modelFilePath = GetModelFilePath();
            if (modelFilePath == null) return null;

            return Path.ChangeExtension(modelFilePath, @".prj");
        }

        private MetadataXml GetDefaultMetadataXml()
        {
            var modelFilePath = GetModelFilePath();
            if (string.IsNullOrWhiteSpace(modelFilePath)) return null;

            var modelFolderPath = Path.GetDirectoryName(modelFilePath);
            if (string.IsNullOrWhiteSpace(modelFolderPath)) return null;

            var metadataXmlFilePath = Path.Combine(modelFolderPath, MetadataXml.FILE_NAME);
            if (File.Exists(metadataXmlFilePath) == false) return null;

            if (MetadataXml.TryParse(metadataXmlFilePath, out var meta) == false)
            {
                return null;
            }

            return meta;
        }

        public string GetDefaultOffsetFilePath()
        {
            var modelFilePath = GetModelFilePath();
            if (modelFilePath == null) return null;

            return Path.ChangeExtension(modelFilePath, @".prjoffset");
        }

        public bool SaveProjFile(string filePath, string projDefinition)
        {
            if (CheckProjDefinition(projDefinition, out var projWkt))
            {
                projWkt.ToUnixFormat().SaveToTextFile(filePath);
                return true;
            }

            return false;
        }

        public bool SaveOffsetFile(string filePath, ProjOffsetType offsetType, double[] offset)
        {
            var json = new JObject();
            json[@"Offset"] = new JArray(offset);
            json[@"OffsetType"] = offsetType.ToString();

            json
                .ToString(Formatting.Indented)
                .ToUnixFormat()
                .SaveToTextFile(filePath);
            return true;
        }

        public OriginType[] GetSupportOriginTypes()
        {
            var originTypes = _Adapter.GetSupportOriginTypes();
            if (originTypes == null || originTypes.Length == 0)
            {
                originTypes = new[] { OriginType.Internal };
            }
            return originTypes;
        }

        public bool IsTrueNorth(OriginType originType)
        {
            return _Adapter.IsTrueNorth(originType);
        }

        public GeoreferencedSetting CreateSuitedSetting(GeoreferencedSetting setting)
        {
            var s = setting?.Clone() ?? CreateDefaultSetting();

            if (_Adapter.IsLocal())
            {
                if (s.Auto != null) s.Auto.Origin = OriginType.Internal;
                if (s.Enu != null) s.Enu.Origin = OriginType.Internal;
                if (s.Local != null) s.Local.Origin = OriginType.Internal;
                if (s.Proj != null) s.Proj.Origin = OriginType.Internal;
            }

            var siteInfo = GetModelSiteInfo();
            if (s.Enu != null && s.Enu.UseProjectLocation)
            {
                s.Enu.Latitude = siteInfo.Latitude;
                s.Enu.Longitude = siteInfo.Longitude;
                s.Enu.Height = siteInfo.Height;
                s.Enu.Rotation = IsTrueNorth(s.Enu.Origin) ? 0.0 : siteInfo.Rotation;
            }
            if (s.Local!= null && s.Local.UseProjectLocation)
            {
                s.Local.Latitude = siteInfo.Latitude;
                s.Local.Longitude = siteInfo.Longitude;
                s.Local.Height = siteInfo.Height;
                s.Local.Rotation = IsTrueNorth(s.Local.Origin) ? 0.0 : siteInfo.Rotation;
            }

            if (s.Proj != null)
            {
                var p = s.Proj;
                switch (p.DefinitionSource)
                {
                    case ProjSourceType.Custom: //人工指定
                        break;
                    case ProjSourceType.Embed:  //项目内置
                    {
                        var projDefinition = _Adapter.GetEmbedProjDefinition();
                        if (string.IsNullOrWhiteSpace(projDefinition))
                        {
                            //如果没有项目内置投影信息，则变更为人工指定
                            p.DefinitionSource = 0;
                            p.DefinitionFileName = null;
                        }
                        else
                        {
                            p.Definition = projDefinition;
                        }
                        break;
                    }
                    case ProjSourceType.Default: //项目默认
                    {
                        var projDefinition = GetDefaultProjDefinition();
                        if (string.IsNullOrWhiteSpace(projDefinition))
                        {
                            //如果没有项目内置投影信息，则变更为人工指定
                            p.DefinitionSource = 0;
                            p.DefinitionFileName = null;
                        }
                        else
                        {
                            p.Definition = projDefinition;

                            if (TryGetDefaultOffset(out var offsetType, out var offset))
                            {
                                p.OffsetType = offsetType;
                                p.Offset = offset;
                            }
                            else
                            {
                                p.OffsetType = ProjOffsetType.None;
                                p.Offset = null;
                            }
                        }
                        break;
                    }
                }
            }

            return s;
        }

        public GeoreferencedSetting CreateDefaultSetting()
        {
            var internalOnly = _Adapter.IsLocal();
            var site = GetModelSiteInfo();

            var setting = new GeoreferencedSetting();
            setting.Mode = GeoreferencedMode.Enu;
            setting.Auto = new ParameterAuto
            {
                Origin = internalOnly ? OriginType.Internal : OriginType.Auto
            };
            setting.Enu = new ParameterEnu
            {
                Origin = internalOnly ? OriginType.Internal : OriginType.Project,
                OriginOffset = null,
                AlignOriginToSitePlaneCenter = true,
                Latitude = site.Latitude,
                Longitude = site.Longitude,
                Height = site.Height,
                Rotation = site.Rotation,
                UseProjectLocation =  !internalOnly,
                UseAutoAlignToGround = false,
            };
            setting.Local = new ParameterLocal
            {
                Origin = internalOnly ? OriginType.Internal : OriginType.Project,
                OriginOffset = null,
                AlignOriginToSitePlaneCenter = true,
                Latitude = site.Latitude,
                Longitude = site.Longitude,
                Height = site.Height,
                Rotation = site.Rotation,
                UseProjectLocation = !internalOnly
            };
            setting.Proj = CreateParameterProj(internalOnly);

            // metadata.xml 优先级最高
            var metadataXml = GetDefaultMetadataXml();
            if (metadataXml != null)
            {
                if (metadataXml.Enu != null)
                {
                    if (_Adapter.IsRevit() && internalOnly == false)
                    {
                        //对于 Revit 项目来说，如果使用了 metadata.xml, 则模型坐标系使用共享坐标系
                        setting.Enu.Origin = OriginType.Shared;
                        setting.Local.Origin = OriginType.Shared;
                    }

                    setting.Enu.AlignOriginToSitePlaneCenter = false;
                    setting.Enu.OriginOffset = metadataXml.Enu.SrsOrigin?.CloneArray();
                    setting.Enu.UseProjectLocation = true;
                    setting.Enu.Latitude = metadataXml.Enu.EnuOrigin[0];
                    setting.Enu.Longitude = metadataXml.Enu.EnuOrigin[1];
                    setting.Enu.Height = metadataXml.Enu.EnuOrigin[2];
                    setting.Enu.Rotation = 0.0;

                    setting.Local.AlignOriginToSitePlaneCenter = false;
                    setting.Local.OriginOffset = metadataXml.Enu.SrsOrigin?.CloneArray();
                    setting.Local.UseProjectLocation = true;
                    setting.Local.Latitude = metadataXml.Enu.EnuOrigin[0];
                    setting.Local.Longitude = metadataXml.Enu.EnuOrigin[1];
                    setting.Local.Height = metadataXml.Enu.EnuOrigin[2];
                    setting.Local.Rotation = 0.0;

                    setting.Mode = GeoreferencedMode.Enu;
                }

                if (metadataXml.Proj != null)
                {
                    setting.Proj.DefinitionSource = ProjSourceType.MetadataXml;
                    setting.Proj.DefinitionFileName = metadataXml.FilePath;
                    setting.Proj.Definition = metadataXml.Proj.Srs;

                    setting.Proj.Offset = metadataXml.Proj.SrsOrigin?.CloneArray();

                    setting.Mode = GeoreferencedMode.Proj;
                }
            }

            return setting;
        }

        public GeoreferencedSetting CreateTargetSetting(GeoreferencedSetting setting)
        {
            var s = setting?.Clone() ?? CreateDefaultSetting();
            if (s.Mode == GeoreferencedMode.Auto)
            {
                //获取目标原点类型
                var targetOriginType = GetSupportOriginTypes().Contains(s.Auto.Origin)
                    ? s.Auto.Origin
                    : OriginType.Auto;

                //自动模式下获取默认设置
                s = CreateDefaultSetting();

                //如果存在有效的内置或默认 proj 定义, 则按照 proj 模式返回
                if (s.Proj != null)
                {
                    if (s.Proj.DefinitionSource == ProjSourceType.Embed ||
                        s.Proj.DefinitionSource == ProjSourceType.Default ||
                        s.Proj.DefinitionSource == ProjSourceType.MetadataXml)
                    {
                        var result = new GeoreferencedSetting();
                        result.Mode = GeoreferencedMode.Proj;
                        result.Proj = s.Proj.Clone();

                        if (targetOriginType != OriginType.Auto)
                        {
                            result.Proj.Origin = targetOriginType;
                        }

                        return CreateSuitedSetting(result);
                    }
                }

                //按照 ENU 模式返回
                {
                    var result = new GeoreferencedSetting();
                    result.Mode = GeoreferencedMode.Enu;
                    result.Enu = s.Enu.Clone();

                    if (targetOriginType != OriginType.Auto)
                    {
                        result.Enu.Origin = targetOriginType;
                    }

                    return CreateSuitedSetting(result);
                }
            }

            return CreateSuitedSetting(s);
        }

        public GeoreferencedSetting CreateTargetSettingForCLI(GeoreferencedSetting setting)
        {
            var s = setting?.Clone() ?? CreateDefaultSetting();

            switch (s.Mode)
            {
                case GeoreferencedMode.Auto:
                    //s.Auto = null;
                    s.Enu = null;
                    s.Local = null;
                    s.Proj = null;
                    break;
                case GeoreferencedMode.Enu:
                    s.Auto = null;
                    //s.Enu = null;
                    s.Local = null;
                    s.Proj = null;
                    break;
                case GeoreferencedMode.Local:
                    s.Auto = null;
                    s.Enu = null;
                    //s.Local = null;
                    s.Proj = null;
                    break;
                case GeoreferencedMode.Proj:
                    s.Auto = null;
                    s.Enu = null;
                    s.Local = null;
                    //s.Proj = null;
                    break;
            }

            return CreateSuitedSetting(s);
        }


        public bool CheckInProjFile(string filePath)
        {
            return _Adapter.CheckInProjFile(filePath);
        }

        public SiteInfo GetModelSiteInfo()
        {
            return GetDefaultMetadataXml()?.GetSiteInfo() ??
                   _Adapter.GetSiteInfo() ?? 
                   GetDefaultSiteInfo();
        }

        public double[] GetDefaultModelOrigin()
        {
            var metadataXml = GetDefaultMetadataXml();

            return metadataXml?.Enu?.SrsOrigin?.CloneArray() ??
                   new[] { 0.0, 0.0, 0.0 };
        }

        public bool ShowPickPositionDialog()
        {
            var previewAppPath = Path.Combine(
                _HomeFolder,
                @"Tools",
                @"Browser",
                @"Bimangle.ForgeBrowser.exe"
            );
            if (File.Exists(previewAppPath) == false) return false;

            try
            {

                Process.Start(previewAppPath, @"--PickPosition");
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }

            return false;
        }

        public Adapter Adapter => _Adapter;

        public bool TestRun(ParameterProj p, double[] dataModel, out double[] dataProjected, out double[] dataWorld)
        {
            var projLibPath = Adapter.GetProjLibPath(_HomeFolder);
            return Adapter.TestRun(projLibPath, p, dataModel, out dataProjected, out dataWorld);
        }

        #endregion

        /// <summary>
        /// 获得默认场地信息
        /// </summary>
        /// <returns></returns>
        private SiteInfo GetDefaultSiteInfo()
        {
            return SiteInfo.CreateDefault();    //默认返回北京坐标
        }

        /// <summary>
        /// 获取默认的投影坐标定义
        /// </summary>
        /// <returns></returns>
        private string GetDefaultProjDefinition()
        {
            var filePath = GetDefaultProjFilePath();
            return GetProjDefinition(filePath);
        }

        /// <summary>
        /// 获取默认的模型坐标变换参数
        /// </summary>
        /// <returns></returns>
        private bool TryGetDefaultOffset(out ProjOffsetType offsetType, out double[] offset)
        {
            var filePath = GetDefaultOffsetFilePath();
            return TryGetOffset(filePath, out offsetType, out offset);
        }

        /// <summary>
        /// 获取项目文件夹内的地理参考定义文件路径
        /// </summary>
        /// <returns></returns>
        private IDictionary<string, string> GetProjectProjFiles()
        {
            var results = new Dictionary<string, string>();

            var projectFilePath = GetModelFilePath();
            if (projectFilePath != null)
            {
                var projectFolderPath = Path.GetDirectoryName(projectFilePath);
                if (string.IsNullOrWhiteSpace(projectFolderPath) == false)
                {
                    var filePaths = Directory.GetFiles(projectFolderPath, @"*.prj", SearchOption.TopDirectoryOnly);
                    foreach (var filePath in filePaths)
                    {
                        var ext = Path.GetExtension(filePath).ToLower();
                        if (ext != @".prj") continue;

                        var fileContent = File.ReadAllText(filePath);
                        if (CheckProjDefinition(fileContent, out _))
                        {
                            var fileName = Path.GetFileName(filePath);
                            results.Add(fileName, fileContent);
                        }
                    }
                }
            }

            return results;
        }

        private bool TryGetOffset(string projOffsetFilePath, out ProjOffsetType offsetType, out double[] offset)
        {
            offsetType = ProjOffsetType.Auto;
            offset = null;

            try
            {
                if (string.IsNullOrWhiteSpace(projOffsetFilePath)) return false;
                if (File.Exists(projOffsetFilePath) == false) return false;

                var content = File.ReadAllText(projOffsetFilePath, Encoding.UTF8);
                var json = JObject.Parse(content);
                var offsetValues = json.Value<JArray>(@"Offset")?.Values<double>()?.ToList();
                var parameterCount = offsetValues != null && offsetValues.Count >= 7 ? 7 : 3;
                offset = new double[parameterCount];
                for (var i = 0; i < parameterCount; i++)
                {
                    offset[i] = 0.0;
                }
                if (offsetValues != null && offsetValues.Count > 0)
                {
                    for (var i = 0; i < parameterCount; i++)
                    {
                        offset[i] = offsetValues[i];
                    }
                }

                var offsetTypeValue = json.Value<string>(@"OffsetType");
                if (Enum.TryParse(offsetTypeValue, true, out offsetType) == false)
                {
                    offsetType = ProjOffsetType.None;
                }

                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }

            return false;
        }

        private ParameterProj CreateParameterProj(bool isFamily)
        {
            //默认从模型坐标变换参数文件中加载数据
            if (TryGetDefaultOffset(out var offsetType, out var offset) == false)
            {
                offsetType = ProjOffsetType.None;
                offset = null;
            }

            var proj = new ParameterProj
            {
                Origin = isFamily ? OriginType.Internal : OriginType.Shared,
                DefinitionSource = ProjSourceType.Custom,
                DefinitionFileName = null,
                Definition = null,
                OffsetType = offsetType,
                Offset = offset
            };

            //大地水准面高校正
            {
                proj.GeoidGrid = null;
                proj.GeoidConstantOffset = 0.0;
            }

            #region 先尝试加载默认投影定义
            {
                var projDefinition = GetDefaultProjDefinition();
                if (projDefinition != null)
                {
                    proj.DefinitionSource = ProjSourceType.Default;
                    proj.DefinitionFileName = null;
                    proj.Definition = projDefinition;
                    return proj;
                }
            }
            #endregion

            #region 再尝试加载项目内置的 proj 定义
            {
                var projDefinition = _Adapter.GetEmbedProjDefinition();
                if (projDefinition != null)
                {
                    proj.DefinitionSource = ProjSourceType.Embed;
                    proj.DefinitionFileName = null;
                    proj.Definition = projDefinition;
                    return proj;
                }
            }
            #endregion

            #region 最后尝试加载项目文件夹内的唯一 *.prj 文件
            {
                var projDefinitions = GetProjectProjFiles();
                if (projDefinitions != null && projDefinitions.Count == 1)
                {
                    proj.DefinitionSource = ProjSourceType.ProjectFolder;
                    proj.DefinitionFileName = projDefinitions.First().Key;
                    proj.Definition = projDefinitions.First().Value;
                }
            }
            #endregion

            return proj;
        }
    }
}
