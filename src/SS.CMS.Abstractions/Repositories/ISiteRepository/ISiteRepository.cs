﻿using System.Collections.Generic;
using System.Threading.Tasks;
using SS.CMS.Data;
using SS.CMS.Enums;
using SS.CMS.Models;
using SS.CMS.Services;

namespace SS.CMS.Repositories
{
    public interface ISiteRepository : IRepository
    {
        Task<int> InsertAsync(Site siteInfo);

        Task<bool> DeleteAsync(int siteId);

        Task<bool> UpdateAsync(Site siteInfo);

        Task UpdateTableNameAsync(int siteId, string tableName);

        Task<List<string>> GetSiteDirListAsync(int parentId);

        Task<List<KeyValuePair<int, Site>>> GetContainerSiteListAsync(string siteName, string siteDir, int startNum, int totalNum, ScopeType scopeType, string orderByString);

        Task<int> GetTableCountAsync(string tableName);

        Task<IEnumerable<int>> GetSiteIdListAsync();

        Task<IList<Site>> GetSiteInfoListAsync();

        Task<IList<Site>> GetSiteInfoListAsync(int parentId);

        Task<Site> GetSiteInfoAsync(int siteId);

        Task<Site> GetSiteInfoBySiteNameAsync(string siteName);

        Task<Site> GetSiteInfoByIsRootAsync();

        Task<int> GetSiteIdByIsRootAsync();

        Task<Site> GetSiteInfoBySiteDirAsync(string siteDir);

        Task<int> GetSiteIdBySiteDirAsync(string siteDir);

        Task<List<int>> GetSiteIdListOrderByLevelAsync();

        Task GetAllParentSiteIdListAsync(List<int> parentSiteIds, List<int> siteIdCollection, int siteId);

        Task<bool> IsExistsAsync(int siteId);

        Task<List<string>> GetSiteTableNamesAsync(IPluginManager pluginManager);

        Task<List<string>> GetAllTableNameListAsync(IPluginManager pluginManager);

        Task<List<string>> GetTableNameListAsync(IPluginManager pluginManager, Site siteInfo);

        Task<int> GetSiteLevelAsync(int siteId);

        Task<int> GetParentSiteIdAsync(int siteId);

        Task<string> GetSiteNameAsync(Site siteInfo);
    }
}