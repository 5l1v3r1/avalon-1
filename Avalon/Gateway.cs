using System;
using System.IO;
using System.Web;
using System.Net;
using System.Text;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

using Avalon.Entities;

namespace Avalon
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
        public CookieContainer Session { get; } = new CookieContainer();

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
        private readonly HtmlParser _parser;

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
                CookieContainer = Session
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

            _parser = new HtmlParser();
        }

        /// <summary>
        /// Overload constructor, create a new instance of <see cref="Gateway"/> with existent cookies based on a <see cref="CookieContainer"/>.
        /// </summary>
        /// <param name="session"></param>
        public Gateway(CookieContainer session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// Try to do Facebook authentication.
        /// </summary>
        /// <exception cref="Exception">On unexpected response from Facebook server.</exception>
        /// <exception cref="InvalidCredentialException">On invalid user account.</exception>
        public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            HttpRequestMessage request;
            HttpResponseMessage response;

            if (Session.Count == 0)
            {
#if DEBUG
                Debug.WriteLine("No cookies found, refreshing...");
#endif

                request = new HttpRequestMessage(HttpMethod.Get, "https://mbasic.facebook.com/")
                {
                    Headers =
                    {
                        {"User-Agent", _userAgent},
                        {"Accept-Language", "pt-BR,pt;q=0.8,en-US;q=0.5,en;q=0.3"}
                    }
                };

                response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Unexpected response code.");
            }

            request = new HttpRequestMessage(HttpMethod.Post,
                "https://mbasic.facebook.com/login/device-based/regular/login/?refsrc=https://mbasic.facebook.com")
            {
                Headers =
                {
                    {"User-Agent", _userAgent},
                    {"Referer", "https://mbasic.facebook.com/"},
                    {"Accept-Language", "pt-BR,pt;q=0.8,en-US;q=0.5,en;q=0.3"}
                },
                Content = new StringContent(
                    $"email={HttpUtility.UrlEncode(MailAddress)}&pass={HttpUtility.UrlEncode(_password)}&login=Entrar",
                    Encoding.UTF8, "application/x-www-form-urlencoded")
            };

            response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new Exception("Unexpected response code.");

            var userId = Session
                .GetCookies(new Uri("https://facebook.com"))
                .OfType<Cookie>()
                .ToList();

            if (userId.All(c => c.Name != "c_user"))
                throw new InvalidCredentialException("Invalid Facebook account credentials!");
        }

        public async Task<ICollection<Group>> GetGroupInformationAsync(CancellationToken cancellationToken = default)
        {
            var groups = new List<Group>();

            var request = new HttpRequestMessage(HttpMethod.Get,
                "https://mbasic.facebook.com/groups/?seemore")
            {
                Headers =
                {
                    {"User-Agent", _userAgent},
                    {"Referer", "https://mbasic.facebook.com/"},
                    {"Accept-Language", "pt-BR,pt;q=0.8,en-US;q=0.5,en;q=0.3"}
                }
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var soup = await response.Content.ReadAsStringAsync();

            var content = await _parser.ParseDocumentAsync(soup);

            var groupsTable = content
                .QuerySelectorAll("table")
                .Where(e => e.InnerHtml.Contains("/groups/") &&
                            !e.InnerHtml.Contains("/groups/create/") &&
                            e.HasAttribute("role") &&
                            e.GetAttribute("role") == "presentation")
                .ToList();

            foreach (var group in groupsTable)
            {
                var tableContent = group
                    .QuerySelectorAll("tbody > tr > td")
                    .ToList();

                if (tableContent.Count != 2) continue;

                var dirtyUrl = tableContent[0]
                    .InnerHtml
                    .Replace("<a href=\"", string.Empty)
                    .Replace("</a>", string.Empty);

                var url = dirtyUrl
                    .Remove(dirtyUrl.IndexOf("\">", StringComparison.Ordinal),
                        dirtyUrl.Length - dirtyUrl.IndexOf("\">", StringComparison.Ordinal));

                var name = dirtyUrl
                    .Remove(0, dirtyUrl[dirtyUrl.IndexOf("\">", StringComparison.Ordinal)])
                    .Trim();

                var notifications = tableContent[1]
                    .InnerHtml
                    .Replace("</span>", string.Empty)
                    .Replace("<span class=", string.Empty)
                    .Replace(">", string.Empty);

                if (!string.IsNullOrEmpty(notifications))
                {
                    notifications = notifications
                        .Remove(notifications.IndexOf("\"", StringComparison.Ordinal),
                            notifications.LastIndexOf("\"", StringComparison.Ordinal) + 1)
                        .Trim();

                    if (int.TryParse(notifications, out var notificationsUpdate))
                        groups.Add(new Group
                        {
                            Url = url,
                            Name = name,
                            Notifications = notificationsUpdate
                        });
                }
                else
                {
                    groups.Add(new Group
                    {
                        Url = url,
                        Name = name,
                        Notifications = 0
                    });
                }
            }

            return groups;
        }

        public async Task NukeAccountAsync(CancellationToken cancellationToken = default)
        {
            var links = await GetAllTimelilePagesAsync(cancellationToken);

            foreach (var link in links)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, link)
                {
                    Headers =
                    {
                        {"User-Agent", _userAgent},
                        {"Referer", "https://mbasic.facebook.com/"},
                        {"Accept-Language", "pt-BR,pt;q=0.8,en-US;q=0.5,en;q=0.3"}
                    }
                };

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Unexpected response code.");

                var soup = await response.Content.ReadAsStringAsync();
                var content = await _parser.ParseDocumentAsync(soup);

                var container = content
                    .QuerySelector("div")
                    .InnerHtml;

                content = await _parser.ParseDocumentAsync(container);

                var posts = content
                    .All
                    .Where(e => e.HasAttribute("role") &&
                                e.HasAttribute("data-ft"))
                    .ToList();

                foreach (var deleteLink in from post in posts
                    select post
                        .QuerySelectorAll("a")
                        .Cast<IHtmlAnchorElement>()
                        .FirstOrDefault(e => e.Href.Contains("/nfx/basic/direct_actions/"))
                    into anchor
                    where anchor != null
                    select anchor
                        .Href
                        .Replace("about:///", "https://mbasic.facebook.com/"))
                {
                    request = new HttpRequestMessage(HttpMethod.Get, deleteLink)
                    {
                        Headers =
                        {
                            {"User-Agent", _userAgent},
                            {"Referer", "https://mbasic.facebook.com/"},
                            {"Accept-Language", "pt-BR,pt;q=0.8,en-US;q=0.5,en;q=0.3"}
                        }
                    };

                    response = await _httpClient.SendAsync(request, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                        throw new Exception("Unexpected response code.");

                    soup = await response.Content.ReadAsStringAsync();
                    content = await _parser.ParseDocumentAsync(soup);

                    var formAction = "https://mbasic.facebook.com" + content
                        .QuerySelector("form")
                        .GetAttribute("action");

                    var fbDtsg = content
                        .QuerySelectorAll("input")
                        .Cast<IHtmlInputElement>()
                        .First(e => e.GetAttribute("name") == "fb_dtsg")
                        .Value;

                    var jazoest = content
                        .QuerySelectorAll("input")
                        .Cast<IHtmlInputElement>()
                        .First(e => e.GetAttribute("name") == "jazoest")
                        .Value;

                    var postData = $"fb_dtsg={HttpUtility.UrlEncode(fbDtsg)}&jazoest={jazoest}&action_key=DELETE";

                    request = new HttpRequestMessage(HttpMethod.Post, formAction)
                    {
                        Headers =
                        {
                            {"User-Agent", _userAgent},
                            {"Referer", deleteLink},
                            {"Accept-Language", "pt-BR,pt;q=0.8,en-US;q=0.5,en;q=0.3"}
                        },
                        Content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded")
                    };

                    await _httpClient.SendAsync(request, cancellationToken);
#if DEBUG
                    Debug.WriteLine("Post deleted!");
#endif
                }
            }
        }

        #region Private methods

        private async Task<ICollection<string>> GetAllTimelilePagesAsync(CancellationToken cancellationToken = default)
        {
            var links = new List<string>();
            HttpRequestMessage request;
            HttpResponseMessage response;

            while (true)
            {
                var target = links.Any()
                    ? links.Last()
                    : "https://mbasic.facebook.com/profile.php";

                request = new HttpRequestMessage(HttpMethod.Get, target)
                {
                    Headers =
                    {
                        {"User-Agent", _userAgent},
                        {"Referer", "https://mbasic.facebook.com/"},
                        {"Accept-Language", "pt-BR,pt;q=0.8,en-US;q=0.5,en;q=0.3"}
                    }
                };

                response = await _httpClient.SendAsync(request, cancellationToken);
                var soup = await response.Content.ReadAsStringAsync();

                if (!soup.Contains("/profile/timeline/stream/?cursor"))
                    break;

                var content = await _parser.ParseDocumentAsync(soup);
                var anchor = content
                    .QuerySelectorAll("a")
                    .Cast<IHtmlAnchorElement>()
                    .First(e => e.InnerHtml.Contains("Ver mais histórias"));

                var link = anchor.Href
                    .Replace("&amp;", "&")
                    .Replace("about:///", "https://mbasic.facebook.com/");

                links.Add(link);
            }

            links.Insert(0, "https://mbasic.facebook.com/profile.php");
            return links;
        }

        #endregion
    }
}