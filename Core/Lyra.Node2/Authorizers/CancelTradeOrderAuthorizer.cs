﻿using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.API;
using Lyra.Core.Accounts.Node;
using Lyra.Node2.Services;
using Lyra.Core.Protos;

namespace Lyra.Node2.Authorizers
{
    public class CancelTradeOrderAuthorizer: BaseAuthorizer
    {
        TradeMatchEngine _TradeMatchEngine;

        public CancelTradeOrderAuthorizer(ServiceAccount serviceAccount, IAccountCollection accountCollection, TradeMatchEngine tradeMatchEngine) : base(serviceAccount, accountCollection)
        {
            _TradeMatchEngine = tradeMatchEngine;
        }

        public override APIResultCodes Authorize<T>(ref T tblock)
        {
            if (!(tblock is CancelTradeOrderBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as CancelTradeOrderBlock;

            // 1. check if the account exists
            if (!_accountCollection.AccountExists(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            TransactionBlock lastBlock = _accountCollection.FindLatestBlock(block.AccountID);
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            var result = VerifyBlock(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            result = VerifyTransactionBlock(block);
            if (result != APIResultCodes.Success)
                return result;

            var original_order = _accountCollection.FindBlockByHash(block.AccountID, block.TradeOrderId) as TradeOrderBlock;
            if (original_order == null)
                return APIResultCodes.NoTradesFound;

            result = ValidateCancellationBalance(block, lastBlock, original_order);
            if (result != APIResultCodes.Success)
                return result;

            Sign(ref block);

            _accountCollection.AddBlock(block);

            _TradeMatchEngine.RemoveOrder(original_order);

            return base.Authorize(ref tblock);
        }

        // The cancellation should restore the balance that was locked by the trade order.
        // Thus, it should take the balance from the latest block and add the balamce (transactin amount) locked by the order block.
        APIResultCodes ValidateCancellationBalance(CancelTradeOrderBlock block, TransactionBlock lastBlock, TradeOrderBlock original_order)
        {
            var order_previous_block = _accountCollection.FindBlockByHash(original_order.PreviousHash);

            var order_transaction = original_order.GetTransaction(order_previous_block);
            var cancel_transaction = block.GetTransaction(lastBlock);

            if (order_transaction.TotalBalanceChange != cancel_transaction.TotalBalanceChange || order_transaction.TokenCode != cancel_transaction.TokenCode)
                return APIResultCodes.CancelTradeOrderValidationFailed;

            return APIResultCodes.Success;
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.NoFee)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != 0)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
        }

    }
}
