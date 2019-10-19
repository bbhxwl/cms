﻿using System.Collections.Generic;
using System.Linq;
using SiteServer.CMS.Core;
using SiteServer.CMS.DataCache.Core;
using SiteServer.CMS.Model;
using SiteServer.CMS.Model.Enumerations;
using SiteServer.Utils;
using SiteServer.Utils.Enumerations;

namespace SiteServer.CMS.DataCache.Content
{
    public static partial class ContentManager
    {
        private static class ListCache
        {
            private static readonly object LockObject = new object();
            private static readonly string CachePrefix = DataCacheManager.GetCacheKey(nameof(ContentManager), nameof(ListCache));

            private static string GetCacheKey(int channelId, int adminId)
            {
                return $"{CachePrefix}.{channelId}.{adminId}";
            }

            public static void Remove(int channelId)
            {
                lock(LockObject)
                {
                    DataCacheManager.RemoveByPrefix($"{CachePrefix}.{channelId}.");
                }
            }

            public static List<int> GetContentIdList(int channelId, int adminId)
            {
                lock (LockObject)
                {
                    var cacheKey = GetCacheKey(channelId, adminId);
                    var list = DataCacheManager.Get<List<int>>(cacheKey);
                    if (list != null) return list;

                    list = new List<int>();
                    DataCacheManager.Insert(cacheKey, list);
                    return list;
                }
            }

            public static void Add(ChannelInfo channelInfo, ContentInfo contentInfo)
            {
                if (ETaxisTypeUtils.Equals(ETaxisType.OrderByTaxisDesc, channelInfo.Additional.DefaultTaxisType))
                {
                    var contentIdList = GetContentIdList(channelInfo.Id, 0);
                    contentIdList.Insert(0, contentInfo.Id);

                    contentIdList = GetContentIdList(channelInfo.Id, contentInfo.AdminId);
                    contentIdList.Insert(0, contentInfo.Id);
                }
                else
                {
                    Remove(channelInfo.Id);
                }
            }

            public static bool IsChanged(ChannelInfo channelInfo, ContentInfo contentInfo1, ContentInfo contentInfo2)
            {
                if (contentInfo1.IsTop != contentInfo2.IsTop) return true;

                var orderAttributeName =
                    ETaxisTypeUtils.GetContentOrderAttributeName(
                        ETaxisTypeUtils.GetEnumType(channelInfo.Additional.DefaultTaxisType));

                return contentInfo1.Get(orderAttributeName) != contentInfo2.Get(orderAttributeName);
            }
        }

        public static List<(int ChannelId, int ContentId)> GetChannelContentIdList(SiteInfo siteInfo, ChannelInfo channelInfo, int adminId, bool isAllContents, int offset, int limit)
        {
            var tableName = ChannelManager.GetTableName(siteInfo, channelInfo);

            var channelContentIdList = new List<(int ChannelId, int ContentId)>();
            var list = ListCache.GetContentIdList(channelInfo.Id, adminId);
            foreach (var contentId in list)
            {
                channelContentIdList.Add((channelInfo.Id, contentId));
            }
            if (isAllContents)
            {
                var channelIdList = ChannelManager.GetChannelIdList(channelInfo, EScopeType.Descendant);
                foreach (var contentChannelId in channelIdList)
                {
                    var contentChannelInfo = ChannelManager.GetChannelInfo(siteInfo.Id, contentChannelId);
                    var channelTableName = ChannelManager.GetTableName(siteInfo, contentChannelInfo);
                    if (!StringUtils.EqualsIgnoreCase(tableName, channelTableName)) continue;

                    list = ListCache.GetContentIdList(contentChannelId, adminId);
                    foreach (var contentId in list)
                    {
                        channelContentIdList.Add((contentChannelId, contentId));
                    }
                }
            }

            if (channelContentIdList.Count >= offset + limit)
            {
                return channelContentIdList.Skip(offset).Take(limit).ToList();
            }

            if (list.Count == offset)
            {
                var dict = ContentCache.GetContentDict(channelInfo.Id);

                var pageContentInfoList = DataProvider.ContentDao.GetContentInfoList(tableName, DataProvider.ContentDao.GetCacheWhereString(siteInfo, channelInfo, adminId, isAllContents),
                    DataProvider.ContentDao.GetOrderString(channelInfo, string.Empty, isAllContents), offset, limit);

                foreach (var contentInfo in pageContentInfoList)
                {
                    dict[contentInfo.Id] = contentInfo;
                }

                var pageContentIdList = pageContentInfoList.Select(x => (x.ChannelId, x.Id)).ToList();
                channelContentIdList.AddRange(pageContentIdList);
                return pageContentIdList;
            }

            return DataProvider.ContentDao.GetCacheChannelContentIdList(tableName, DataProvider.ContentDao.GetCacheWhereString(siteInfo, channelInfo, adminId, isAllContents),
                DataProvider.ContentDao.GetOrderString(channelInfo, string.Empty, isAllContents), offset, limit);
        }
    }
}