﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using Bimangle.ForgeEngine.Common.Georeferenced;
using Newtonsoft.Json;

namespace Bimangle.ForgeEngine.Navisworks.Config
{
    [Serializable]
    [DataContract]
    class JobConfig
    {
        /// <summary>
        /// 目标数据格式
        /// </summary>
        [DataMember(EmitDefaultValue = false, Name = "format")]
        public string Format { get; set; }

        /// <summary>
        /// 转化模式
        /// </summary>
        /// <remarks>
        /// 目前只对 Cesium 3D Tiles 格式有效
        /// </remarks>
        [DataMember(EmitDefaultValue = false, Name = "mode")]
        public int Mode { get; set; }

        /// <summary>
        /// 输出目标文件夹路径
        /// </summary>
        [DataMember(EmitDefaultValue = false, Name = "outputPath")]
        public string OutputPath { get; set; }

        /// <summary>
        /// 输出选项
        /// </summary>
        [DataMember(EmitDefaultValue = false, Name = "outputOptions")]
        public List<string> OutputOptions { get; set; }

        /// <summary>
        /// 详细程度
        /// </summary>
        [DataMember(EmitDefaultValue = false, Name = "levelOfDetail")]
        public int LevelOfDetail { get; set; }

        /// <summary>
        /// 详细程度
        /// </summary>
        [DataMember(EmitDefaultValue = false, Name = "levelOfDetailAssigned")]
        public bool LevelOfDetailAssigned { get; set; }

        /// <summary>
        /// 地理配准设置
        /// </summary>
        [DataMember(EmitDefaultValue = false, Name = "georeferenced")]
        public GeoreferencedSetting GeoreferencedSetting { get; set; }

        /// <summary>
        /// 加载任务配置
        /// </summary>
        /// <param name="jobFilePath"></param>
        /// <returns></returns>
        public static JobConfig Load(string jobFilePath)
        {
            if (File.Exists(jobFilePath) == false) return null;

            var json = File.ReadAllText(jobFilePath, Encoding.UTF8);
            var jobConfig = JsonConvert.DeserializeObject<JobConfig>(json);
            return jobConfig;
        }

        public int GetLevelOfDetail()
        {
            return LevelOfDetailAssigned == false
                ? -1
                : LevelOfDetail;
        }
    }
}
