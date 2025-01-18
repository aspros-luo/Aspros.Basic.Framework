using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Text;

namespace Aspros.Base.Framework.Infrastructure
{
    public class RewriteQueryStringMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Method == "POST" || context.Request.Method == "PUT")
            {
                if (context.Request.ContentType.Contains("application/json"))
                {
                    #region 修改请求的body 

                    var requestBodyStream = new MemoryStream(); //创建一个流 
                    //设置当前流的位置未0
                    requestBodyStream.Seek(0, SeekOrigin.Begin); //设置从0开始读取
                    //这里ReadToEnd执行完毕后requestBodyStream流的位置会从0到最后位置(即request.ContentLength)
                    var requestBody = new StreamReader(context.Request.Body).ReadToEnd(); //读取body
                    //需要将流位置重置偏移到0，不然后续的action读取不到request.Content的值 
                    requestBodyStream.Seek(0, SeekOrigin.Begin);

                    var newDics = new Dictionary<string, dynamic>();
                    var dics = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(requestBody);
                    foreach (var dic in dics)
                    {
                        newDics.Add(dic.Key.ToPascalCase(), dic.Value);
                    }

                    var str = JsonConvert.SerializeObject(newDics);

                    var content1 = Encoding.UTF8.GetBytes(str); //替换字符串并且字符串转换成字节
                    requestBodyStream.Seek(0, SeekOrigin.Begin);
                    requestBodyStream.Write(content1, 0, content1.Length); //把修改写入流中
                    context.Request.Body = requestBodyStream; //把修改后的内容赋值给请求body
                    context.Request.Body.Seek(0, SeekOrigin.Begin);

                    #endregion
                }
            }

            if (context.Request.Method == "GET")
            {
                var query = context.Request.QueryString;
                if (query.HasValue)
                {
                    var parms = string.Join("&", query.Value.TrimStart('?').Split('&').Select(s =>
                    {
                        var kv = s.Split('=');
                        var k = kv[0].Replace("_", "");
                        var v = kv[1];
                        return $"{k}={v}";
                    }));
                    var newQuery = new QueryString($"?{parms}");
                    context.Request.QueryString = newQuery;
                }
            }

            //Let the next middleware (MVC routing) handle the request
            //In case the path was updated, the MVC routing will see the updated path
            await _next.Invoke(context);
        }
    }

    public static class RewriteQueryStringExtensions
    {
        public static IApplicationBuilder UseRewriteQueryString(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RewriteQueryStringMiddleware>();
        }
    }
}