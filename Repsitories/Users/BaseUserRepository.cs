/* ------------------------------------------------------------------------- *
thZero.NetCore.Library.Data.Repository.MongoDb.App
Copyright (C) 2016-2022 thZero.com

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

using Nito.AsyncEx;

using thZero.Data;
using thZero.Data.Repository.MongoDb;
using thZero.Instrumentation;
using thZero.Responses.Users;

namespace thZero.Repositories.Users
{
    public abstract class BaseUserRepository<TRepository, TUserResponse, TUserData, TUserSetting, TPlanData> : BaseMongoDbRepository<TRepository, TUserData, BaseData>
        where TUserResponse : BaseUserResponse<TUserData, TUserSetting, TPlanData>
        where TUserData : BaseUserData<TUserSetting, TPlanData>
        where TPlanData : BasePlanData
    {
        public BaseUserRepository(IOptions<MongoDbRepositoryConnectionConfiguration> config, ILogger<TRepository> logger) : base(config, logger)
        {
        }

        #region Public Methods
        public async Task<TUserResponse> FetchAsync(IInstrumentationPacket instrumentation, string userId, bool excludePlan = false)
        {
            Enforce.AgainstNull(() => instrumentation);
            Enforce.AgainstNullOrEmpty(() => userId);

            TUserResponse response = Instantiate(instrumentation);

            response.Results = await GetCollectionUsers().Collection.Find(filter => filter.Id.Equals(userId)).Project<TUserData>(DefaultProjectionBuilder<TUserData>()).SingleOrDefaultAsync();
            if (response.Results == null)
                return Error(response);

            if (!excludePlan)
            {
                if ((response.Results == null) || ((response.Results != null) && String.IsNullOrEmpty(response.Results.PlanId)))
                    return Error(response, "Missing PlanId");

                response.Results.Plan = await GetCollectionPlans().Collection.Find(filter => filter.Id.Equals(response.Results.PlanId)).Project<TPlanData>(DefaultProjectionBuilder<TPlanData>()).FirstOrDefaultAsync();
            }

            return response;
        }

        public async Task<TUserResponse> FetchByExternalIdAsync(IInstrumentationPacket instrumentation, string externalUserId, bool excludePlan = false)
        {
            Enforce.AgainstNull(() => instrumentation);
            Enforce.AgainstNullOrEmpty(() => externalUserId);

            TUserResponse response = Instantiate(instrumentation);

            var filter = Builders<TUserData>.Filter.Eq(x => x.External.Id, externalUserId);
            //response.Results = await GetCollectionUsers().Find(filter => filter.External.Id.Equals(externalUserId)).SingleOrDefaultAsync();
            response.Results = await GetCollectionUsers().Collection.Find(filter).Project<TUserData>(DefaultProjectionBuilder<TUserData>()).SingleOrDefaultAsync();
            if (response.Results == null)
                return Error(response);

            if (!excludePlan)
            {
                if ((response.Results == null) || ((response.Results != null) && String.IsNullOrEmpty(response.Results.PlanId)))
                    return Error(response, "Missing PlanId");

                response.Results.Plan = await GetCollectionPlans().Collection.Find(filter => filter.Id.Equals(response.Results.PlanId)).Project<TPlanData>(DefaultProjectionBuilder<TPlanData>()).FirstOrDefaultAsync();
            }

            return response;
        }

        public async Task RefreshSettingsAsync(IInstrumentationPacket instrumentation, object parameters)
        {
            throw new NotImplementedException();
        }

        private static readonly AsyncReaderWriterLock _mutex = new();

        public async Task<TUserResponse> UpdateFromExternalAsync(IInstrumentationPacket instrumentation, string userId, TUserData user)
        {
            Enforce.AgainstNull(() => instrumentation);
            Enforce.AgainstNullOrEmpty(() => userId);
            Enforce.AgainstNull(() => user);

            const string Declaration = "UpdateFromExternalAsync";

            try
            {
                TUserResponse response = Instantiate(instrumentation);

                user.UpdatedTimestamp = thZero.Utilities.DateTime.Timestamp;

                var collectionUsers = GetCollectionUsers();
                using (var session = collectionUsers.Client.StartSession())
                {
                    try
                    {
                        session.StartTransaction();

                        // TODO: transaction
                        var find = await collectionUsers.Collection.Find(filter => filter.Id.Equals(userId)).Project<TUserData>(DefaultProjectionBuilder<TUserData>()).FirstOrDefaultAsync();
                        if (find == null)
                        {
                            try
                            {
                                await collectionUsers.Collection.InsertOneAsync(user);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError2(Declaration, ex);
                                {
                                    session.AbortTransaction();
                                    return Error(response, "Invalid user update.");
                                }
                            }
                        }
                        else
                        {
                            ReplaceOneResult results = await collectionUsers.Collection.ReplaceOneAsync(filter => filter.Id.Equals(userId), user, UpsertOptions);
                            if (results.ModifiedCount <= 0)
                            {
                                session.AbortTransaction();
                                return Error(response, "Invalid user update.");
                            }
                        }

                        // TODO: transaction
                        response.Results = await collectionUsers.Collection.Find(filter => filter.Id.Equals(userId)).Project<TUserData>(DefaultProjectionBuilder<TUserData>()).FirstOrDefaultAsync();
                        if (response.Results == null)
                        {
                            session.AbortTransaction();
                            return Error(response);
                        }

                        session.CommitTransaction();

                        response.Results = user;
                        return response;
                    }
                    catch (Exception)
                    {
                        session.AbortTransaction();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError2(Declaration, ex);
                throw;
            }
        }

        public async Task UpdateSettingsAsync(IInstrumentationPacket instrumentation, object requestedSettings)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Protected Methods
        protected abstract MongoCollectionResponse<TPlanData> GetCollectionPlans();
        protected abstract MongoCollectionResponse<TUserData> GetCollectionUsers();

        protected abstract TUserResponse Instantiate(IInstrumentationPacket instrumentation);

        protected ProjectionDefinition<TSource> GamerProjectionBuilder<TSource>()
            where TSource : TUserData
        {
            return Builders<TSource>.Projection
                .Exclude(x => x._id)
                .Exclude(x => x.External)
                .Exclude(x => x.External.Email)
                .Exclude(x => x.External.Picture)
                .Exclude(x => x.Roles)
                .Exclude(x => x.PlanId);
        }
        protected ProjectionDefinition<TSource> PlayerProjectionBuilder<TSource>()
            where TSource : TUserData
        {
            return Builders<TSource>.Projection
                .Exclude(x => x._id)
                .Exclude(x => x.Roles)
                .Exclude(x => x.PlanId);
        }
        #endregion
    }
}
