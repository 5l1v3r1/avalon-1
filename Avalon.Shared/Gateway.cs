using System;
using System.Net;
using System.Text;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Avalon.Shared
{
    /// <summary>
    /// Exposes a authentication wrapper for Facebook.
    /// </summary>
    public class Gateway
    {
        /// <summary>
        /// Current mail address.
        /// </summary>
        public string MailAddress { get; }

        /// <summary>
        /// Current Facebook session.
        /// </summary>
        public CookieContainer CookieContainer { get; } = new CookieContainer();

        private readonly string[] _userAgents =
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.157 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.110 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_14_4) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3782.0 Safari/537.36 Edg/76.0.152.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3794.0 Safari/537.36 Edg/76.0.162.0",
            "Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/42.0.2311.135 Safari/537.36 Edge/19.10136"
        };
        private readonly string _userAgent;
        private readonly string _password;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Default constructor, creates a new instance of <see cref="Gateway"/>.
        /// </summary>
        /// <param name="mailAddress">Facebook account e-mail address.</param>
        /// <param name="password">Account password.</param>
        public Gateway(string mailAddress, string password)
        {
            MailAddress = mailAddress ?? throw new ArgumentNullException(nameof(mailAddress));
            _password = password ?? throw new ArgumentNullException(nameof(password));

            _userAgent = _userAgents[new Random().Next(0, _userAgents.Length)];

#if DEBUG
            _httpClient = new HttpClient(new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                UseProxy = true,
                Proxy = new WebProxy("127.0.0.1:8080"),
                ClientCertificates = {new X509Certificate2(Path.Combine(Environment.CurrentDirectory, "cacert.der"))},
                UseCookies = true,
                AllowAutoRedirect = true,
                CookieContainer = CookieContainer
            });
#else
            _httpClient = new HttpClient(new HttpClientHandler()
            {

                UseCookies = true,
                AllowAutoRedirect = true,
                CookieContainer = CookieContainer
            });
#endif

#if DEBUG
            Debug.WriteLine($"Current user agent is \"{_userAgent}\"");
#endif
        }

        /// <summary>
        /// Try to do Facebook authentication.
        /// </summary>
        /// <exception cref="Exception">On unexpected response from Facebook server.</exception>
        /// <exception cref="InvalidCredentialException">On invalid user account.</exception>
        public async Task AuthenticateAsync()
        {
            HttpRequestMessage request;
            HttpResponseMessage response;

            if (CookieContainer.Count == 0)
            {
#if DEBUG
                Debug.WriteLine("No cookies found, refreshing...");
#endif

                request = new HttpRequestMessage(HttpMethod.Get, "https://mbasic.facebook.com/")
                {
                    Headers =
                    {
                        {"User-Agent", _userAgent }
                    }
                };

                response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Unexpected response code.");
            }

            request = new HttpRequestMessage(HttpMethod.Post, "https://mbasic.facebook.com/login/device-based/regular/login/")
            {
                Headers =
                {
                    {"User-Agent", _userAgent },
                    {"Referer", "https://mbasic.facebook.com/" },
                    {"Host", "mbasic.facebook.com" }
                },
                Content = new StringContent($"email={MailAddress}&pass={_password}&login=Entrar", Encoding.UTF8, "application/x-www-form-urlencoded")
            };

            response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                throw new Exception("Unexpected response code.");

            if (CookieContainer
                .GetCookies(new Uri("https://facebook.com"))
                .OfType<Cookie>()
                .Any(cookie => cookie.Name != "c_user"))
                throw new InvalidCredentialException("Invalid Facebook account credentials!");
        }
    }
}
