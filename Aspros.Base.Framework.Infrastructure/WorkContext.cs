using Aspros.Base.Framework.Infrastructure.Const;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;


namespace Aspros.Base.Framework.Infrastructure
{
    public class WorkContext(IHttpContextAccessor contextAccessor, IDistributedCache cache) : IWorkContext
    {
        private readonly IHttpContextAccessor _contextAccessor = contextAccessor;
        private readonly IDistributedCache _cache = cache;

        public async Task<T> Get<T>(string key)
        {
            var tokenJson = await GetUserData();
            return tokenJson[key].ToObject<T>();
        }

        public async Task<long> GetTenantId()
        {
            var tokenJson = await GetUserData();
            if (tokenJson == null) return 0;
            else return tokenJson["tenant_id"] == null ? 0 : tokenJson["tenant_id"].ToObject<long>();
        }

        public async Task<long> GetUserId()
        {
            var tokenJson = await GetUserData();
            if (tokenJson == null) return 0;
            else return tokenJson["user_id"] == null ? 0 : tokenJson["user_id"].ToObject<long>();

        }

        private async Task<JObject> GetUserData()
        {
            var token = _contextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(token)) return null;

            token = token.Replace("Bearer ", "");
            string userDataString;
            var result = MD5.HashData(Encoding.UTF8.GetBytes(token));
            var strResult = BitConverter.ToString(result);
            string userKey = $"{CacheConst.Token}{strResult.Replace("-", "")}";
            var userDataByte = await _cache.GetAsync(userKey);
            if (userDataByte != null)
            {
                userDataString = Encoding.UTF8.GetString(userDataByte);
                return JsonConvert.DeserializeObject<JObject>(userDataString);
            }
            else
            {
                var json = Jose.JWT.Payload(token);
                var obj = JObject.Parse(json);
                userDataString = JsonConvert.SerializeObject(obj);
                await _cache.SetAsync(userKey, Encoding.UTF8.GetBytes(userDataString), new DistributedCacheEntryOptions().SetAbsoluteExpiration(DateTimeOffset.Now.AddHours(5)));
                return obj;
            }
        }
    }
}
