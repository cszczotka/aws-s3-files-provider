using Syncfusion.EJ2.FileManager.AmazonS3FileProvider;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Syncfusion.EJ2.FileManager.Base;
using Amazon;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;

namespace EJ2AmazonS3ASPCoreFileProvider.Controllers
{

    [Route("api/[controller]")]
    [EnableCors("AllowAllOrigins")]
    public class AmazonS3ProviderController : Controller
    {
        public AmazonS3FileProvider operation;
        public string basePath;
        protected RegionEndpoint bucketRegion;
        private readonly IConfiguration config;

        public AmazonS3ProviderController(IHostEnvironment env, IConfiguration config)
        {
            this.config = config;
            this.basePath = env.ContentRootPath;
            this.operation = new AmazonS3FileProvider();
            /**
             *  To overwrite appsettings by env variables uses such env variable name "S3:Name", "S3:AccessKey", "S3:SecretKey"
             */
            var s3config = config.GetSection("S3");
            var bucket = s3config.GetSection("Name").Value;
            var accessKey = s3config.GetSection("AccessKey").Value;
            var secretKey = s3config.GetSection("SecretKey").Value;
            var region = s3config.GetSection("Region").Value;
            this.operation.RegisterAmazonS3(bucket, accessKey, secretKey, region);
        }
        
        [Route("AmazonS3FileOperations")]
        [HttpPost]
        [Authorize]
        public object AmazonS3FileOperations([FromBody] FileManagerDirectoryContent args)
        {
            if (args.Action == "delete" || args.Action == "rename")
            {
                if ((args.TargetPath == null) && (args.Path == ""))
                {
                    FileManagerResponse response = new FileManagerResponse();
                    ErrorDetails er = new ErrorDetails
                    {
                        Code = "401",
                        Message = "Restricted to modify the root folder."
                    };
                    response.Error = er;
                    return this.operation.ToCamelCase(response);
                }
            }
            switch (args.Action)
            {
                case "read":
                    // reads the file(s) or folder(s) from the given path.
                    return this.operation.ToCamelCase(this.operation.GetFiles(args.Path, false, args.Data));
                case "delete":
                    // deletes the selected file(s) or folder(s) from the given path.
                    return this.operation.ToCamelCase(this.operation.Delete(args.Path, args.Names, args.Data));
                case "copy":
                    // copies the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                    return this.operation.ToCamelCase(this.operation.Copy(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));
                case "move":
                    // cuts the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                    return this.operation.ToCamelCase(this.operation.Move(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));
                case "details":
                    // gets the details of the selected file(s) or folder(s).
                    return this.operation.ToCamelCase(this.operation.Details(args.Path, args.Names, args.Data));
                case "create":
                    // creates a new folder in a given path.
                    return this.operation.ToCamelCase(this.operation.Create(args.Path, args.Name, args.Data));
                case "search":
                    // gets the list of file(s) or folder(s) from a given path based on the searched key string.
                    return this.operation.ToCamelCase(this.operation.Search(args.Path, args.SearchString, args.ShowHiddenItems, args.CaseSensitive, args.Data));
                case "rename":
                    // renames a file or folder.
                    return this.operation.ToCamelCase(this.operation.Rename(args.Path, args.Name, args.NewName, false, args.Data));
            }
            return null;
        }

        // uploads the file(s) into a specified path
        [Route("AmazonS3Upload")]
        [HttpPost]
        [Authorize]
        public IActionResult AmazonS3Upload([FromForm, Required] string path, IList<IFormFile> uploadFiles, [FromForm, Required] string action, [FromForm, Required] string data)
        {
            FileManagerResponse uploadResponse;
            
            if(String.IsNullOrEmpty(path))
            {
                Console.WriteLine("Error -> required parameter path is null or empty");
                return BadRequest("Required parameter path is null or empty");
            }

            if (String.IsNullOrEmpty(action))
            {
                Console.WriteLine("Error -> required parameter action is null or empty");
                return BadRequest("Required parameter action is null or empty");
            }

            if (String.IsNullOrEmpty(data))
            {
                Console.WriteLine("Error -> required parameter data is null or empty");
                return BadRequest("Required parameter data is null or empty");
            }

            if(uploadFiles == null || uploadFiles.Count == 0)
            {
                Console.WriteLine("Error -> no upload files");
                return BadRequest("No upload files");
            }
            try
            {
                FileManagerDirectoryContent[] dataObject = new FileManagerDirectoryContent[1];
                dataObject[0] = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(data);
                foreach (var file in uploadFiles)
                {
                    var folders = (file.FileName).Split('/');
                    // checking the folder upload
                    if (folders.Length > 1)
                    {
                        for (var i = 0; i < folders.Length - 1; i++)
                        {
                            if (!this.operation.checkFileExist(path, folders[i]))
                            {
                                this.operation.ToCamelCase(this.operation.Create(path, folders[i], dataObject));
                            }
                            path += folders[i] + "/";
                        }
                    }
                }
                uploadResponse = operation.Upload(path, uploadFiles, action, dataObject);
                if (uploadResponse.Error != null)
                {
                    Response.Clear();
                    Response.ContentType = "application/json; charset=utf-8";
                    Response.StatusCode = Convert.ToInt32(uploadResponse.Error.Code);
                    Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = uploadResponse.Error.Message;
                }
                return Content("");
            } 
            catch(Exception e)
            {
                Console.WriteLine("Error during S3 upload: " + e.Message);
                return StatusCode(500, e.Message);
            }
        }

        // downloads the selected file(s) and folder(s)
        [Route("AmazonS3Download")]
        [HttpPost]
        [Authorize]
        public IActionResult AmazonS3Download(string downloadInput)
        {
            FileManagerDirectoryContent args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(downloadInput);
            return operation.Download(args.Path, args.Names);
        }

        // gets the image(s) from the given path
        [Route("AmazonS3GetImage")]
        [HttpGet]
        [Authorize]
        public IActionResult AmazonS3GetImage(FileManagerDirectoryContent args)
        {
            return operation.GetImage(args.Path, args.Id, false, null, args.Data);
        }

    }

}
