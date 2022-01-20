/* ------------------------------------------------------------------------- *
thZero.NetCore.Library.Data.Repository.MongoDb.App
Copyright (C) 2016-2021 thZero.com

<development [at] thzero [dot] com>

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
 * ------------------------------------------------------------------------- */

using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

using thZero.Data;
using thZero.Data.Repository.MongoDb;
using thZero.Instrumentation;
using thZero.Responses.News;

namespace thZero.Repositories.News
{
    public abstract class BaseNewsRepository<TRepository, TNewsResponse, TNewsData> : BaseMongoDbRepository<TRepository, TNewsData, BaseData>
        where TNewsResponse : BaseNewsResponse<TNewsData>
        where TNewsData : BaseNewsData
    {
        public BaseNewsRepository(IOptions<MongoDbRepositoryConnectionConfiguration> config, ILogger<TRepository> logger) : base(config, logger)
        {
        }

        #region Public Methods
        public async Task<TNewsResponse> LastestAsync(IInstrumentationPacket instrumentation, long timestamp)
        {
            return InstantiateResponse(instrumentation); // TODO
        }
        #endregion

        #region Protected Methods
        protected abstract MongoCollectionResponse<TNewsData> GetCollectionNews();

        protected abstract TNewsResponse InstantiateResponse(IInstrumentationPacket instrumentation);
        #endregion
    }
}
