using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.IO;
using Newtonsoft.Json;
using Syncfusion.EJ2.FileManager.Base;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace AWSS3FileProvider.Middleware
{
    public class AuthzMiddleware
    {
        private readonly RequestDelegate _next;
       
        public AuthzMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            var endpoint = context.Request.Path.Value;
            var queryString = context.Request.QueryString.Value;
            if (token != null)
            {
                var jwtToken = new JwtSecurityToken(token);
                string gci = jwtToken.Claims.First(c => c.Type == "custom:GCI").Value;
                if (!String.IsNullOrEmpty(gci))
                {
                    if ("1".Equals(gci))
                    {
                        await _next(context);
                    } 
                    else
                    {
                        string pathParam = await getPath(endpoint, context);
                        if(pathParam.StartsWith("/" + gci + "/"))
                        {
                            await _next(context);
                        }
                        else
                        {
                            context.Response.StatusCode = 401;
                            await context.Response.WriteAsync("Unauthorized to access path " + pathParam);
                        }
                    }
                    
                } 
                else
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Unauthorized");
                }                
            } else
            {
                await _next(context);
            }              
                   
        }

        private async Task<string> getPath(string endpoint, HttpContext context)
        {

            if(endpoint.Equals("/api/AmazonS3Provider/AmazonS3FileOperations") )
            {
                var body = await GetRequestBody(context);
                FileManagerDirectoryContent args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(body);
                return args.Path;
            }
            else if(endpoint.Equals("/api/AmazonS3Provider/AmazonS3Upload"))
            {
                var form = await context.Request.ReadFormAsync();
                var pathParam = form["path"];
                return pathParam;
            }
            else if (endpoint.Equals("/api/AmazonS3Provider/AmazonS3Download"))
            {
                var body = await GetRequestBody(context);
                FileManagerDirectoryContent args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(body);
                return args.Path;

            }
            else if (endpoint.Equals("/api/AmazonS3Provider/AmazonS3GetImage"))
            {
                StringValues path = "";
                var queryString = context.Request.Query.TryGetValue("path", out path);
                return path;
            }

            return "";
        }

        private async Task<string> GetRequestBody(HttpContext context)
        {
            RecyclableMemoryStreamManager recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
            await using var requestStream = recyclableMemoryStreamManager.GetStream();
            await context.Request.Body.CopyToAsync(requestStream);
            var body = ReadStreamInChunks(requestStream);
            return body;
        }

        private static string ReadStreamInChunks(Stream stream)
        {
            const int readChunkBufferLength = 4096;
            stream.Seek(0, SeekOrigin.Begin);
            using var textWriter = new StringWriter();
            using var reader = new StreamReader(stream);
            var readChunk = new char[readChunkBufferLength];
            int readChunkLength;
            do
            {
                readChunkLength = reader.ReadBlock(readChunk,
                                                   0,
                                                   readChunkBufferLength);
                textWriter.Write(readChunk, 0, readChunkLength);
            } while (readChunkLength > 0);
            return textWriter.ToString();
        }

    }

    public static class AuthzMiddlewareExtension
    {
        public static IApplicationBuilder UseAuthzMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthzMiddleware>();
        }
    }



}
