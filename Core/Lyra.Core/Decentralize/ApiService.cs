using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Fees;
using Lyra.Exchange;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyra.Core.Utils;
using System.Threading;
using Akka.Actor;
using System.Linq;
using Lyra.Shared;

namespace Lyra.Core.Decentralize
{
    public class ApiService : INodeTransactionAPI//, IBlockConsensus
    {
        private readonly ILogger<ApiService> _log;

        long _useed = -1;

        public ApiService(ILogger<ApiService> logger)
        {
            _log = logger;
        }

        //public async Task OnActivateAsync()
        //{
        //    _log.LogInformation("ApiService: Activated");
        //    _useed = await NodeService.Dag.Storage.GetBlockCount();

        //    //await Gossip(new ChatMsg($"LyraNode[{_config.Orleans.EndPoint.AdvertisedIPAddress}]", $"Startup. IsSeedNode: {IsSeedNode}"));
        //}

        public long GenerateUniversalBlockIdAsync()
        {
            // if self master, use seeds; if not, ask master node
            return _useed++;
        }

        public async Task<BillBoard> GetBillBoardAsync()
        {
            return await NodeService.Dag.Consensus.Ask<BillBoard>(new ConsensusService.AskForBillboard());
        }

        public async Task<List<TransStats>> GetTransStatsAsync()
        {
            return await NodeService.Dag.Consensus.Ask<List<TransStats>>(new ConsensusService.AskForStats());
        }

        public async Task<string> GetDbStats()
        {
            return await NodeService.Dag.Consensus.Ask<string>(new ConsensusService.AskForDbStats());
        }

        private async Task<AuthState> PostToConsensusAsync(TransactionBlock block)
        {
            _log.LogInformation($"ApiService: PostToConsensusAsync Called: {block.BlockType}");

            AuthorizingMsg msg = new AuthorizingMsg
            {
                IsServiceBlock = false,
                From = NodeService.Dag.PosWallet.AccountId,
                Block = block,
                BlockHash = block.Hash,
                MsgType = ChatMessageType.AuthorizerPrePrepare
            };

            var state = new AuthState(true);            
            state.InputMsg = msg;

            NodeService.Dag.Consensus.Tell(state);

            await state.Done.AsTask();
            state.Done.Close();
            state.Done = null;

            var ts1 = state.T1 == null ? "" : ((int)(DateTime.Now - state.T1).TotalMilliseconds).ToString();
            var ts2 = state.T2 == null ? "" : ((int)(DateTime.Now - state.T2).TotalMilliseconds).ToString();
            var ts3 = state.T3 == null ? "" : ((int)(DateTime.Now - state.T3).TotalMilliseconds).ToString();
            var ts4 = state.T4 == null ? "" : ((int)(DateTime.Now - state.T4).TotalMilliseconds).ToString();
            var ts5 = state.T5 == null ? "" : ((int)(DateTime.Now - state.T5).TotalMilliseconds).ToString();

            _log.LogInformation($"ApiService Timing:\n{ts1}\n{ts2}\n{ts3}\n{ts4}\n{ts5}\n");

            var resultMsg = state.OutputMsgs.Count > 0 ? state.OutputMsgs.First().Result.ToString() : "Unable to authorize block";
            _log.LogInformation($"ApiService: PostToConsensusAsync Exited: IsAuthoringSuccess: {state?.CommitConsensus == ConsensusResult.Yea} with {resultMsg}");
            
            return state;
        }

        internal async Task<AuthorizationAPIResult> Pre_PrepareAsync(TransactionBlock block1, Func<TransactionBlock, Task<TransactionBlock>> OnBlockSucceed = null)
        {
            var result = new AuthorizationAPIResult();

            //AuthState state2 = null;
            var state1 = await PostToConsensusAsync(block1).ConfigureAwait(false);

            if (state1 != null && state1.CommitConsensus == ConsensusResult.Yea)
            {
                result.ResultCode = APIResultCodes.Success;

                ////fee is the bottle neck!!! must do lazy fee collection by consolidation
                //if (OnBlockSucceed != null)
                //{
                //    var block2 = await OnBlockSucceed(state1.InputMsg.Block as TransactionBlock).ConfigureAwait(false);
                //    if(block2 != null)
                //        state2 = await PostToConsensusAsync(block2).ConfigureAwait(false);
                //}
            }
            else if (state1 != null && state1.CommitConsensus == ConsensusResult.Nay)
            {
                result.ResultCode = APIResultCodes.BlockFailedToBeAuthorized;
            }
            else if (state1 != null && state1.CommitConsensus == ConsensusResult.Uncertain)
            {
                result.ResultCode = APIResultCodes.ConsensusTimeout;
            }
            else
            {
                result.ResultCode = state1.OutputMsgs.Count > 0 ? state1.OutputMsgs.First().Result : APIResultCodes.BlockFailedToBeAuthorized;
            }

            return result;
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            return await Pre_PrepareAsync(block).ConfigureAwait(false);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock openReceiveBlock)
        {
            return await Pre_PrepareAsync(openReceiveBlock).ConfigureAwait(false);
        }


        public async Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            return await Pre_PrepareAsync(block).ConfigureAwait(false);
        }

        public async Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock sendBlock)
        {
            return await Pre_PrepareAsync(sendBlock).ConfigureAwait(false);
        }

        public Task<AuthorizationAPIResult> SendExchangeTransfer(ExchangingBlock block)
        {
            return SendTransfer(block);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock receiveBlock)
        {
            return await Pre_PrepareAsync(receiveBlock).ConfigureAwait(false);
        }

        public async Task<AuthorizationAPIResult> ReceiveFee(ReceiveAuthorizerFeeBlock receiveBlock)
        {
            return await Pre_PrepareAsync(receiveBlock).ConfigureAwait(false);
        }

        public async Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            return await Pre_PrepareAsync(block).ConfigureAwait(false);
        }

        public async Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock tokenBlock)
        {
            var result = new AuthorizationAPIResult();

            // filter the names
            if (tokenBlock.DomainName.ToLower().StartsWith("lyra")
                || tokenBlock.Ticker.ToLower().StartsWith("lyra"))
            {
                result.ResultCode = APIResultCodes.NameUnavailable;
                return result;
            }

            return await Pre_PrepareAsync(tokenBlock).ConfigureAwait(false);
        }

        public async Task<ExchangeAccountAPIResult> CreateExchangeAccount(string AccountId, string Signature)
        {
            // TODO verify signature first

            // store to database
            var acct = await NodeService.Dealer.AddExchangeAccount(AccountId);

            var result = new ExchangeAccountAPIResult()
            {
                ResultCode = APIResultCodes.Success,
                AccountId = acct.AccountId
            };
            return result;            
        }

        public async Task<ExchangeBalanceAPIResult> GetExchangeBalance(string AccountId, string Signature)
        {
            var acct = await NodeService.Dealer.GetExchangeAccount(AccountId, true);
            var result = new ExchangeBalanceAPIResult()
            {
                ResultCode = APIResultCodes.Success
            };
            if(acct != null)
            {
                result.AccountId = acct.AccountId;
                result.Balance = acct.Balance;
            }
            return result;
        }
        public async Task<CancelKey> SubmitExchangeOrder(TokenTradeOrder reqOrder)
        {
            CancelKey key;
            var result = reqOrder.VerifySignature(reqOrder.AccountID);
            if (!result
                || reqOrder.TokenName == null 
                || reqOrder.Price <= 0 
                || reqOrder.Amount <= 0)
            {
                key = new CancelKey() { Key = string.Empty, State = OrderState.BadOrder };
                return key;
            }

            // verify the balance. 
            var acct = await NodeService.Dealer.GetExchangeAccount(reqOrder.AccountID, true);
            if(acct == null)
            {
                return new CancelKey() { Key = string.Empty, State = OrderState.BadOrder };
            }

            if (reqOrder.BuySellType == OrderType.Sell)
            {                
                if(acct.Balance.ContainsKey(reqOrder.TokenName) && acct.Balance[reqOrder.TokenName] < reqOrder.Amount)
                    return new CancelKey() { Key = string.Empty, State = OrderState.InsufficientFunds };
            }
            else
            {
                // buy order
                if(acct.Balance.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) && acct.Balance[LyraGlobal.OFFICIALTICKERCODE] < reqOrder.Amount * reqOrder.Price)
                    return new CancelKey() { Key = string.Empty, State = OrderState.InsufficientFunds };
            }
                
            return await NodeService.Dealer.AddOrderAsync(acct, reqOrder);
        }

        public async Task<APIResult> CancelExchangeOrder(string AccountId, string Signature, string cancelKey)
        {
            await NodeService.Dealer.RemoveOrderAsync(cancelKey);
            return new APIResult() { ResultCode = APIResultCodes.Success };
        }

        public Task<ExchangeAccountAPIResult> CloseExchangeAccount(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        Task<APIResult> CustomizeNotifySettings(NotifySettings settings)
        {
            throw new NotImplementedException();
        }

        //#region Fee processing private methods

        //async Task<(APIResultCodes result, TransactionBlock block)> ProcessTransferFee(SendTransferBlock sendBlock)
        //{
        //    // TO DO: handle all token balances, not just LYRA
        //    if(sendBlock is ExchangingBlock)
        //    {
        //        if(sendBlock.Fee != ExchangingBlock.FEE)
        //            return (APIResultCodes.InvalidFeeAmount, null);
        //    }
        //    else if (sendBlock.Fee != (await NodeService.Dag.Storage.GetLastServiceBlockAsync()).TransferFee)
        //        return (APIResultCodes.InvalidFeeAmount, null);

        //    if(sendBlock.FeeType == AuthorizationFeeTypes.NoFee)
        //        return (APIResultCodes.Success, null);

        //    return await ProcessFee(sendBlock.Hash, sendBlock.Fee);
        //}

        //async Task<(APIResultCodes result, TransactionBlock block)> ProcessTokenGenerationFee(TokenGenesisBlock tokenBlock)
        //{
        //    if (tokenBlock.Fee != (await NodeService.Dag.Storage.GetLastServiceBlockAsync()).TokenGenerationFee)
        //        return (APIResultCodes.InvalidFeeAmount, null);

        //    return await ProcessFee(tokenBlock.Hash, tokenBlock.Fee);
        //}

        //private async Task<(APIResultCodes result, TransactionBlock block)> ProcessFee(string source, decimal fee)
        //{
        //    var callresult = APIResultCodes.Success;
        //    TransactionBlock blockresult = null;

        //    var svcBlockResult = await NodeService.Dag.Storage.GetLastServiceBlockAsync();

        //    TransactionBlock latestBlock = await NodeService.Dag.Storage.FindLatestBlockAsync(NodeService.Dag.PosWallet.AccountId) as TransactionBlock;
        //    if(latestBlock == null)
        //    {
        //        var receiveBlock = new OpenWithReceiveFeeBlock
        //        {
        //            AccountType = AccountTypes.Service,
        //            AccountID = NodeService.Dag.PosWallet.AccountId,
        //            ServiceHash = svcBlockResult.Hash,
        //            SourceHash = source,
        //            Fee = 0,
        //            FeeType = AuthorizationFeeTypes.NoFee,
        //            Balances = new Dictionary<string, long>()
        //        };
        //        receiveBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, fee.ToBalanceLong());
        //        receiveBlock.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, NodeService.Dag.PosWallet.AccountId);

        //        //var authorizer = GrainFactory.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Core.Authorizers.NewAccountAuthorizer");
        //        //callresult = await authorizer.Authorize(receiveBlock);
        //        blockresult = receiveBlock;
        //    }
        //    else
        //    {
        //        var receiveBlock = new ReceiveAuthorizerFeeBlock
        //        {
        //            AccountID = NodeService.Dag.PosWallet.AccountId,
        //            ServiceHash = svcBlockResult.Hash,
        //            SourceHash = source,
        //            Fee = 0,
        //            FeeType = AuthorizationFeeTypes.NoFee,
        //            Balances = new Dictionary<string, long>()
        //        };

        //        decimal newBalance = latestBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] + fee.ToBalanceLong();
        //        receiveBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, newBalance.ToBalanceLong());
        //        receiveBlock.InitializeBlock(latestBlock, NodeService.Dag.PosWallet.PrivateKey, NodeService.Dag.PosWallet.AccountId);

        //        //var authorizer = GrainFactory.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Core.Authorizers.ReceiveTransferAuthorizer");
        //        //callresult = await authorizer.Authorize(receiveBlock);
        //        blockresult = receiveBlock;
        //    }

        //    //receiveBlock.Signature = Signatures.GetSignature(NodeService.Dag.Storage.ServiceAccount.PrivateKey, receiveBlock.Hash);
        //    return (callresult, blockresult);
        //}

        //#endregion

        // util 
        private T FromJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        private string Json(object o)
        {
            return JsonConvert.SerializeObject(o);
        }

        #region Reward Trade Athorization Methods 
        
        public async Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock tradeOrderBlock)
        {
            var result = new TradeOrderAuthorizationAPIResult();

            try
            {
                var auth_result = await Pre_PrepareAsync(tradeOrderBlock).ConfigureAwait(false);

                if (auth_result.ResultCode == APIResultCodes.TradeOrderMatchFound)
                {
                    var result_block = await NodeService.Dag.TradeEngine.Match(tradeOrderBlock);
                    if (result_block != null)
                    {
                        result.ResultCode = APIResultCodes.TradeOrderMatchFound;
                        result.SetBlock(result_block);
                        result.ServiceHash = result_block.ServiceHash;
                    }
                    else
                    {
                        result.ResultCode = APIResultCodes.NoTradesFound;
                    }
                    return result;
                }
                else
                {
                    result.ResultCode = auth_result.ResultCode;
                }
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in TradeOrder: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInTradeOrderAuthorizer;
                result.ResultMessage = e.Message;
            }

            return result;
        }


        public async Task<AuthorizationAPIResult> Trade(TradeBlock block)
        {
            return await Pre_PrepareAsync(block).ConfigureAwait(false);
        }

        public async Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
             return await Pre_PrepareAsync(block).ConfigureAwait(false);
        }

        public async Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block)
        {
            return await Pre_PrepareAsync(block).ConfigureAwait(false);
        }

        #endregion

        public async Task<APIResult> RequestMarket(string tokenName)
        {
            if(tokenName != null)
                await NodeService.Dealer.SendMarket(tokenName);
            return new APIResult() { ResultCode = APIResultCodes.Success };
        }

        public async Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId, string Signature)
        {
            return await NodeService.Dealer.GetOrdersForAccount(AccountId);
        }

        //public override async Task<GetVersionReply> GetVersion(GetVersionRequest request, ServerCallContext context)
        //{
        //    var cr = await GetVersion(request.ApiVersion, request.AppName, request.Appversion);
        //    var result = new GetVersionReply()
        //    {
        //        ApiVersion = cr.ApiVersion,
        //        NodeVersion = cr.NodeVersion,
        //        ResultCode = cr.ResultCode,
        //        UpgradeNeeded = cr.UpgradeNeeded,
        //        MustUpgradeToConnect = cr.MustUpgradeToConnect
        //    };
        //    return result;
        //}

        //public override async Task<AccountHeightReply> GetSyncHeight(SimpleRequest request, ServerCallContext context)
        //{
        //    var cr = await GetSyncHeight();
        //    var result = new AccountHeightReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        NetworkId = cr.NetworkId,
        //        Height = cr.Height,
        //        SyncHash = cr.SyncHash
        //    };
        //    return result;
        //}
        //public override async Task<GetTokenNamesReply> GetTokenNames(GetTokenNamesRequest request, ServerCallContext context)
        //{
        //    var cr = await GetTokenNames(request.AccountId, request.Signature, request.Keyword);
        //    var result = new GetTokenNamesReply()
        //    {
        //        ResultCode = cr.ResultCode
        //    };
        //    result.TokenNames.AddRange(cr.TokenNames);

        //    return result;
        //}
        //public override async Task<GetAccountHeightReply> GetAccountHeight(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await GetAccountHeight(request.AccountId, request.Signature);
        //    var result = new GetAccountHeightReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        Height = cr.Height,
        //        SyncHash = cr.SyncHash,
        //        NetworkId = cr.NetworkId
        //    };
        //    return result;
        //}
        //public override async Task<GetBlockReply> GetBlockByIndex(GetBlockByIndexRequest request, ServerCallContext context)
        //{
        //    var cr = await GetBlockByIndex(request.AccountId, request.Index, request.Signature);
        //    var result = new GetBlockReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        BlockData = cr.BlockData,
        //        ResultBlockType = cr.ResultBlockType
        //    };

        //    return result;
        //}
        //public override async Task<GetBlockReply> GetBlockByHash(GetBlockByHashRequest request, ServerCallContext context)
        //{
        //    var cr = await GetBlockByHash(request.AccountId, request.Hash, request.Signature);
        //    var result = new GetBlockReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        BlockData = cr.BlockData,
        //        ResultBlockType = cr.ResultBlockType
        //    };

        //    return result;
        //}
        //public override async Task<GetNonFungibleTokensReply> GetNonFungibleTokens(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await GetNonFungibleTokens(request.AccountId, request.Signature);
        //    var result = new GetNonFungibleTokensReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ListDataSerialized = cr.ListDataSerialized
        //    };
        //    return result;
        //}
        //public override async Task<GetBlockReply> GetTokenGenesisBlock(GetTokenGenesisBlockRequest request, ServerCallContext context)
        //{
        //    var cr = await GetTokenGenesisBlock(request.AccountId, request.TokenTicker, request.Signature);
        //    var result = new GetBlockReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        BlockData = cr.BlockData,
        //        ResultBlockType = cr.ResultBlockType
        //    };
        //    return result;
        //}
        //public override async Task<GetBlockReply> GetLastServiceBlock(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await GetLastServiceBlock(request.AccountId, request.Signature);
        //    var result = new GetBlockReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        BlockData = cr.BlockData,
        //        ResultBlockType = cr.ResultBlockType
        //    };
        //    return result;
        //}
        //public override async Task<LookForNewTransferReply> LookForNewTransfer(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await LookForNewTransfer(request.AccountId, request.Signature);
        //    LookForNewTransferReply transfer_info = new LookForNewTransferReply()
        //    {
        //        ResultCode = cr.ResultCode
        //    };
        //    if (cr.ResultCode != APIResultCodes.NoNewTransferFound)
        //    {
        //        transfer_info.NonFungibleTokenJson = Json(cr.NonFungibleToken);
        //        transfer_info.SourceHash = cr.SourceHash;
        //        transfer_info.TransferJson = Json(cr.Transfer);
        //    }
        //    return transfer_info;
        //}
        //public override async Task<AuthorizationsReply> OpenAccountWithGenesis(OpenAccountWithGenesisRequest request, ServerCallContext context)
        //{
        //    var openBlock = FromJson<LyraTokenGenesisBlock>(request.OpenTokenGenesisBlockJson);
        //    var cr = await OpenAccountWithGenesis(openBlock);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}
        //public override async Task<AuthorizationsReply> ReceiveTransferAndOpenAccount(ReceiveTransferAndOpenAccountRequest request, ServerCallContext context)
        //{
        //    var openReceiveBlock = FromJson<OpenWithReceiveTransferBlock>(request.OpenReceiveBlockJson);
        //    var cr = await ReceiveTransferAndOpenAccount(openReceiveBlock);
        //    if(cr.ResultCode == APIResultCodes.Success)
        //    {
        //        var result = new AuthorizationsReply()
        //        {
        //            ResultCode = cr.ResultCode,
        //            ServiceHash = cr.ServiceHash ?? string.Empty,
        //            AuthorizationsJson = Json(cr.Authorizations)
        //        };
        //        return result;
        //    }
        //    else
        //    {
        //        var result = new AuthorizationsReply() { ResultCode = cr.ResultCode };
        //        return result;
        //    }

        //}
        //public override async Task<AuthorizationsReply> OpenAccountWithImport(OpenAccountWithImportRequest request, ServerCallContext context)
        //{
        //    var block = FromJson<OpenAccountWithImportBlock>(request.BlockJson);
        //    var cr = await OpenAccountWithImport(block);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}
        //public override async Task<AuthorizationsReply> SendTransfer(SendTransferRequest request, ServerCallContext context)
        //{
        //    var sendBlock = FromJson<SendTransferBlock>(request.SendBlockJson);
        //    var cr = await SendTransfer(sendBlock);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}
        //public override async Task<AuthorizationsReply> SendExchangeTransfer(SendTransferRequest request, ServerCallContext context)
        //{
        //    var sendBlock = FromJson<ExchangingBlock>(request.SendBlockJson);
        //    var cr = await SendTransfer(sendBlock);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}
        //public override async Task<AuthorizationsReply> ReceiveTransfer(ReceiveTransferRequest request, ServerCallContext context)
        //{
        //    var receiveBlock = FromJson<ReceiveTransferBlock>(request.ReceiveBlockJson);
        //    var cr = await ReceiveTransfer(receiveBlock);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}
        //public override async Task<AuthorizationsReply> ImportAccount(ImportAccountRequest request, ServerCallContext context)
        //{
        //    var block = FromJson<ImportAccountBlock>(request.ImportBlockJson);
        //    var cr = await ImportAccount(block);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}


        //public override async Task<AuthorizationsReply> CreateToken(CreateTokenRequest request, ServerCallContext context)
        //{
        //    var tokenBlock = FromJson<TokenGenesisBlock>(request.CreateTokenJson);
        //    var cr = await CreateToken(tokenBlock);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}

        //public override async Task<ExchangeAccountReply> CreateExchangeAccount(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await CreateExchangeAccount(request.AccountId, request.Signature);
        //    var result = new ExchangeAccountReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        AccountId = cr.AccountId
        //    };
        //    return result;
        //}

        //public override async Task<GetExchangeBalanceReply> GetExchangeBalance(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await GetExchangeBalance(request.AccountId, request.Signature);
        //    var result = new GetExchangeBalanceReply()
        //    {
        //        AccountId = cr.AccountId,
        //        ResultCode = cr.ResultCode,
        //        BalanceJson = Json(cr.Balance)
        //    };
        //    return result;
        //}

        //public override async Task<SubmitExchangeOrderReply> SubmitExchangeOrder(SubmitExchangeOrderRequest request, ServerCallContext context)
        //{
        //    var order = FromJson<TokenTradeOrder>(request.TokenTradeOrderJson);
        //    var cr = await SubmitExchangeOrder(order);
        //    var result = new SubmitExchangeOrderReply()
        //    {
        //        CancelKeyJson = Json(cr)
        //    };
        //    return result;
        //}

    }
}
