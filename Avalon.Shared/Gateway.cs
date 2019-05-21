using System;
using System.Net;
using System.Net.Http;
using System.Diagnostics;

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

        private string[] _userAgents =
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
        /// Default constructor, creates a new instance of <see cref="Authenticator"/>.
        /// </summary>
        /// <param name="user">Facebook accoount e-mail address.</param>
        /// <param name="password">Account password.</param>
        public Gateway(string mailAddress, string password)
        {
            MailAddress = mailAddress ?? throw new ArgumentNullException(nameof(mailAddress));
            _password = password ?? throw new ArgumentNullException(nameof(password));

            _userAgent = _userAgents[new Random().Next(0, _userAgents.Length)];

            _httpClient = new HttpClient(new HttpClientHandler()
            {
                UseCookies = true,
                AllowAutoRedirect = true,
                CookieContainer = CookieContainer
            });

#if DEBUG
            Debug.WriteLine($"Current user agent is \"{_userAgent}\"");
#endif
        }
    }
}
