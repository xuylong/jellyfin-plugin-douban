using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Douban
{
    public class ImageProvider : BaseProvider, IRemoteImageProvider, IHasOrder
    {
        public string Name => "Douban Image Provider";
        public int Order => 3;

        public ImageProvider(IHttpClient httpClient,
                             IJsonSerializer jsonSerializer,
                             ILogger<ImageProvider> logger) : base(httpClient, jsonSerializer, logger)
        {
            // empty
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item,
            CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();
            var sid = item.GetProviderId(ProviderID);
            if (string.IsNullOrWhiteSpace(sid))
            {
                // Not by douban
                _logger.LogWarning("GetImages failed because that the sid " +
                    "is empty: {Name}", item.Name);
                return list;
            }
            var primaryList = await GetPrimary(sid, cancellationToken);
            var backdropList = await GetBackdrop(sid, cancellationToken);
            list.AddRange(primaryList);
            list.AddRange(backdropList);

            return list;
        }

        public bool Supports(BaseItem item)
        {
            return item is Movie || item is Series;
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new List<ImageType>
            {
                ImageType.Primary,
                ImageType.Backdrop
            };
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetPrimary(string sid,
            CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();
            var movie = await GetDoubanSubject(sid, cancellationToken);
            list.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Url = movie.Images.Large,
                Type = ImageType.Primary
            });
            return list;
        }
        public async Task<IEnumerable<RemoteImageInfo>> GetBackdrop(string sid,
            CancellationToken cancellationToken)
        {
            var url = string.Format("https://movie.douban.com/subject/{0}/photos?" +
                                    "type=W&start=0&sortby=size&size=a&subtype=a", sid);

            String content = await _doubanAccessor.GetResponseWithDelay(url, cancellationToken);
            const String pattern = @"(?s)data-id=""(\d+)"".*?class=""prop"">\n\s*(\d+)x(\d+)";
            Match match = Regex.Match(content, pattern);

            var list = new List<RemoteImageInfo>();
            while (match.Success)
            {
                string data_id = match.Groups[1].Value;
                string width = match.Groups[2].Value;
                string height = match.Groups[3].Value;
                _logger.LogInformation("Find backdrip id {0}, size {1}x{2}", data_id, width, height);

                if (float.Parse(width) > float.Parse(height) * 1.3)
                {
                    // Just chose the Backdrop which width is larger than height
                    list.Add(new RemoteImageInfo
                    {
                        ProviderName = Name,
                        Url = string.Format("https://img9.doubanio.com/view/photo/l/public/p{0}.webp", data_id),
                        Type = ImageType.Backdrop,
                    });
                }

                match = match.NextMatch();
            }

            return list;
        }
    }
}