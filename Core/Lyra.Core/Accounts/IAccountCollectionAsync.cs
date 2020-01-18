﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyra.Core.Blocks;

namespace Lyra.Core.Accounts
{
    /// <summary>
    /// hole block lists.
    /// </summary>
    public interface IAccountCollectionAsync : IDisposable
    {
        // for sync
        Task<long> GetNewestBlockUIndexAsync();
        Task<TransactionBlock> GetBlockByUIndexAsync(long uindex);

        // for service
        Task<long> GetBlockCountAsync();
        Task<long> GetBlockCountAsync(string AccountId);
        //int GetTotalBlockCount();
        Task<bool> AccountExistsAsync(string AccountId);
        Task<TransactionBlock> FindLatestBlockAsync();
        Task<TransactionBlock> FindLatestBlockAsync(string AccountId);
        Task<TokenGenesisBlock> FindTokenGenesisBlockAsync(string Hash, string Ticker);
        Task<List<TokenGenesisBlock>> FindTokenGenesisBlocksAsync(string keyword);
        Task<NullTransactionBlock> FindNullTransBlockByHashAsync(string hash);
        Task<TransactionBlock> FindBlockByHashAsync(string hash);
        Task<TransactionBlock> FindBlockByHashAsync(string AccountId, string hash);
        Task<ReceiveTransferBlock> FindBlockBySourceHashAsync(string hash);
        Task<List<NonFungibleToken>> GetNonFungibleTokensAsync(string AccountId);
        Task<TransactionBlock> FindBlockByPreviousBlockHashAsync(string previousBlockHash);
        Task<TransactionBlock> FindBlockByIndexAsync(string AccountId, Int64 index);
        Task<SendTransferBlock> FindUnsettledSendBlockAsync(string AccountId);

        // for service blocks
        Task<ServiceBlock> GetLastServiceBlockAsync();
        Task<ConsolidationBlock> GetSyncBlockAsync();

        /// <summary>
        /// Returns the first unexecuted trade aimed to an order created on the account.
        /// </summary>
        /// <param name="AccountId"></param>
        /// <param name="BuyTokenCode">
        /// The code of the token being purchased (optional).
        /// </param>
        /// <param name="SellTokenCode">
        /// The code of the token being sold (optional).
        /// </param>
        /// <returns></returns>
        TradeBlock FindUnexecutedTrade(string AccountId, string BuyTokenCode, string SellTokenCode);

        List<TradeOrderBlock> GetTradeOrderBlocks();

        List<string> GetTradeOrderCancellations();

        // returns the list of hashes (order IDs) of all cancelled trade order blocks
        List<string> GetExecutedTradeOrderBlocks();

        Task<bool> AddBlockAsync(TransactionBlock block);

        /// <summary>
        /// Cleans up or deletes blocks collection.
        /// Used for unit testing.
        /// </summary>
        void Delete();
    }
}