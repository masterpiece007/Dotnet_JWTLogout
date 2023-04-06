using JWTLogout.Net.Models;
//using LiteDB.Async;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using LiteDB;

namespace JWTLogout.Net.Helpers
{
    public class JwtCheck
    {
        //private LiteDatabaseAsync db;
        private LiteDatabase db;

        public JwtCheck()
        {
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "JwtStore.db");
            db = new LiteDatabase(dbPath);
        }
      
        /// <summary>
        /// login: call this function at the tail end of your login method
        /// </summary>
        /// <param name="jwt">the jwt that was just generated,which is to be registered</param>
        /// <returns></returns>
        //public async Task<string> Login(string jwt)
        public string Login(string jwt)
        {
            if (string.IsNullOrEmpty(jwt))
                return "empty jwt";

            var jwtExpiry = FetchJwtExpiry(jwt);
            if (string.IsNullOrEmpty(jwtExpiry))
                return "jwt expiry time not found";
          
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "JwtStore.db");
            using (var db = new LiteDatabase(dbPath))
            {
                var collection = db.GetCollection<TokenStore>();

                var newJwt = new TokenStore
                {
                    IsLoggedOut = false,
                    Jwt = jwt,
                    ExpiryTime = jwtExpiry
                };

                var rowsInserted = collection.Insert(newJwt);
                return "Ok";
            }
        }
     
        ///// <summary>
        ///// login: call this function at the tail end of your login method
        ///// </summary>
        ///// <param name="httpContext">HttpContext that contains the incoming request</param>
        ///// <returns></returns>
        //public async Task<string> Login(HttpContext httpContext)
        //{
        //    var data = FetchJwtAndExpiry(httpContext);
        //    if (data == null)
        //        return "issue with http-context";
        //    var collection = db.GetCollection<TokenStore>();
        //    var newJwt = new TokenStore
        //    {
        //        IsLoggedOut = false,
        //        Jwt = data.Jwt,
        //        ExpiryTime = data.ExpiryDate
        //    };
        //    var rowsInserted = await collection.InsertAsync(newJwt);
        //    return "Ok";
        //}


        /// <summary>
        /// logout: call this function anywhere in your logout endpoint implementation
        /// </summary>
        /// <param name="jwt">jwt to be marked invalid</param>
        /// <returns></returns>
        //public async Task<string> Logout(string jwt)
        public string Logout(string jwt)
        {
            if (string.IsNullOrEmpty(jwt))
                return "empty jwt";

            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "JwtStore.db");
            using (var db = new LiteDatabase(dbPath))
            {
                var collection = db.GetCollection<TokenStore>();
                //var matchingJwts = await collection.Query().Where(a => a.Jwt.ToLower() == jwt.ToLower()).ToListAsync();
                var matchingJwts =  collection.Find(a => a.Jwt.ToLower() == jwt.ToLower()).ToList();

                if (matchingJwts.Count > 0)
                {
                    var rowsUpdated = 0;
                    matchingJwts.ForEach(a =>
                    {
                        a.IsLoggedOut = false;
                        //var isUpdated = collection.UpdateAsync(a).GetAwaiter().GetResult();
                        var isUpdated = collection.Update(a);
                        _ = isUpdated ? ++rowsUpdated : rowsUpdated;
                    });

                    if (rowsUpdated > 0)
                        return "OK";
                    return "Failed";
                }

                return "jwt was never logged";
            }
        }
   
        /// <summary>
        /// logout: call this function anywhere in your logout endpoint implementation
        /// </summary>
        ///  <param name="httpContext">HttpContext that contains the incoming request</param>
        /// <returns></returns>
        //public async Task<string> Logout(HttpContext httpContext)
        public string Logout(HttpContext httpContext)
        {
            var data = FetchJwtAndExpiry(httpContext);
            if (data == null)
                return "issue with http-context";

            var collection = db.GetCollection<TokenStore>();
            //var matchingJwts = await collection.Query().Where(a => a.Jwt.ToLower() == data.Jwt.ToLower()).ToListAsync();
            var matchingJwts =  collection.Find(a => a.Jwt.ToLower() == data.Jwt.ToLower()).ToList();
            if (matchingJwts.Count > 0)
            {
                var rowsUpdated = 0;
                matchingJwts.ForEach(a =>
                {
                    a.IsLoggedOut = true;
                    //var isUpdated = collection.UpdateAsync(a).GetAwaiter().GetResult();
                    var isUpdated = collection.Update(a);
                    _ = isUpdated ? ++rowsUpdated : rowsUpdated;
                });

                if (rowsUpdated > 0)
                    return "OK";
                return "Failed";
            }
            return "jwt was never logged";
        }

        internal bool IsTokenValid(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;
            var now = DateTime.Now;
            var collection = db.GetCollection<TokenStore>();
            //var invalidJwts = await collection.Query()
            //    .Where(a =>
            //        a.Jwt.ToLower() == token.ToLower() &&
            //        (a.IsLoggedOut == true || now > DateTime.Parse(a.ExpiryTime))).ToListAsync();
 
            var invalidJwts =  collection.Find(a =>
                    a.Jwt.ToLower() == token.ToLower() &&
                    (a.IsLoggedOut == true || now > DateTime.Parse(a.ExpiryTime))).ToList();

            if (invalidJwts.Count > 0)
            {
                //var rowsAffected = await collection.DeleteManyAsync(a => now > DateTime.Parse(a.ExpiryTime));
                var rowsAffected =  collection.Delete(a => now > DateTime.Parse(a.ExpiryTime));
                return false;
            }
            return true;
        }
        internal static JwtDto FetchJwtAndExpiry(HttpContext httpContext)
        {
            try
            {

                var jwt = ExtractJwtFromHeader(httpContext);
                if (string.IsNullOrEmpty(jwt))
                    return null;
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(jwt);
                var tokenS = jsonToken as JwtSecurityToken;

                var expiryTime = tokenS.Claims.FirstOrDefault(claim => claim.Type == "exp")?.Value;

                var expiryDate = new DateTime(1970, 1, 1, 0, 0, 0, 0)
                    .AddSeconds(double.Parse(expiryTime)).ToString("MM/dd/yyyy HH:mm:ss");
                return new JwtDto { ExpiryDate = expiryDate, Jwt = jwt };
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        internal static string FetchJwtExpiry(string jwt)
        {
            try
            {
                if (string.IsNullOrEmpty(jwt))
                    return null;

                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(jwt);
                var tokenS = jsonToken as JwtSecurityToken;

                var expiryTime = tokenS.Claims.FirstOrDefault(claim => claim.Type == "exp")?.Value;
                return new DateTime(1970, 1, 1, 0, 0, 0, 0)
                    .AddSeconds(double.Parse(expiryTime))
                    .ToString("MM/dd/yyyy HH:mm:ss");
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        internal static string ExtractJwtFromHeader(HttpContext httpContext)
        {
            var authHeader = httpContext.Request.Headers[HeaderNames.Authorization].ToString();

            if (!authHeader.Contains("Bearer") && !authHeader.Contains("bearer"))
            {
                return null;
            }

            var splitHeader = authHeader.ToString().Split(' ');
            var jwt = splitHeader[1];
            return jwt;
        }

        //Insert new jwt mapped to a user on successful login call.

        //Check if jwt is still valid when request comes in.

        //logout jwt when logout endpoint is called.

        //remove jwt from store when datetime.now exceeds expirytime


    }
}