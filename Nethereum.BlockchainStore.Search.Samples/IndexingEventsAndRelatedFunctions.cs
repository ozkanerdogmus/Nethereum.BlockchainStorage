﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.BlockchainStore.Search.Azure;
using Nethereum.Configuration;
using Nethereum.Contracts;
using Xunit;

namespace Nethereum.BlockchainStore.Search.Samples
{
    [Collection("Nethereum.BlockchainStore.Search.Samples")]
    public class IndexingEventsAndRelatedFunctions
    {
        public const string ApiKeyName = "AzureSearchApiKey";
        public const string AzureSearchServiceName = "blockchainsearch";
        private const string BlockchainUrl = "https://rinkeby.infura.io/v3/25e7b6dfc51040b3bfc0e47317d38f60";
        private readonly string _azureSearchApiKey;

        public const string TransferEventIndexName = "transfer-event";
        public const string TransferFunctionIndexName = "transfer-function";

        public IndexingEventsAndRelatedFunctions()
        {
            //user secrets are only for development
            //if not in development the key will be retrieved from environmental variables or command line args
            ConfigurationUtils.SetEnvironment("development");

            //use the command line to set your azure search api key
            //e.g. dotnet user-secrets set "AzureSearchApiKey" "<put key here>"
            var appConfig = ConfigurationUtils
                .Build(Array.Empty<string>(), userSecretsId: "Nethereum.BlockchainStore.Search.Samples");

            _azureSearchApiKey = appConfig[ApiKeyName];
        }

        [Function("transfer", "bool")]
        public class TransferFunction: FunctionMessage
        {
            [Parameter("address", "_to", 1)]
            public string To {get; set;}
            [Parameter("uint256", "_value", 2)]
            public BigInteger Value {get; set;}
        }

        [Event("Transfer")]
        public class TransferEvent_ERC20
        {
            [Parameter("address", "_from", 1, true)]
            public string From {get; set;}

            [Parameter("address", "_to", 2, true)]
            public string To {get; set;}

            [Parameter("uint256", "_value", 3, false)]
            public BigInteger Value {get; set;}
        }

        /// <summary>
        /// Indexes matching events
        /// Also indexes related functions
        /// </summary>
        [Fact]
        public async Task IndexTransferEventAndFunction()
        {
            using (var processor =
                new AzureEventIndexingProcessor(AzureSearchServiceName, _azureSearchApiKey, BlockchainUrl))
            {
                #region test preparation
                await processor.ClearProgress();
                await processor.SearchService.DeleteIndexAsync(TransferEventIndexName);
                await processor.SearchService.DeleteIndexAsync(TransferFunctionIndexName);
                #endregion

                var transferFunctionProcessor =
                    await processor.CreateFunctionProcessorAsync<TransferFunction>(TransferFunctionIndexName);

                await processor.AddIndexer<TransferEvent_ERC20>(
                    TransferEventIndexName, new IEventFunctionProcessor[]
                {
                    transferFunctionProcessor
                });

                await processor.ProcessAsync(3146684, 3146694);

                //allow time for azure indexing to finish
                await Task.Delay(TimeSpan.FromSeconds(5));

                //ensure we have written to the transfer event index
                var eventIndexer = await processor.SearchService.GetOrCreateEventIndexer<TransferEvent_ERC20>(TransferEventIndexName);
                Assert.Equal(19, await eventIndexer.DocumentCountAsync());

                //ensure we have written to the transfer function index
                var functionIndexer = await processor.SearchService.GetOrCreateFunctionIndexer<TransferFunction>(TransferFunctionIndexName);
                Assert.Equal(2, await functionIndexer.DocumentCountAsync());

                #region test clean up 
                await processor.ClearProgress();
                await processor.SearchService.DeleteIndexAsync(TransferEventIndexName);
                await processor.SearchService.DeleteIndexAsync(TransferFunctionIndexName);
                #endregion
            }
        }
    }
}
