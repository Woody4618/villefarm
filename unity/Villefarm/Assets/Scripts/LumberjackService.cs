using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Frictionless;
using Lumberjack;
using Lumberjack.Accounts;
using Lumberjack.Program;
using Solana.Unity.Programs;
using Solana.Unity.Programs.Models;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using SolPlay.Scripts.Services;
using UnityEngine;


    public class LumberjackService : MonoBehaviour
    {
        public PublicKey LumberjackProgramIdPubKey = new PublicKey("9eSWUTsPxc3HEVCKe1oHBo7fXPSua5dHZkN48k2Q8yyL");
        
        public const int TIME_TO_GROW = 20;
        public const int MAX_ENERGY = 10;
        
        public static LumberjackService Instance { get; private set; }
        public static Action<PlayerData> OnPlayerDataChanged;
        public static Action<Plot> OnPlotDataChanged;
        public static Action OnInitialDataLoaded;
        
        private SessionWallet sessionWallet;
        private PublicKey PlayerDataPDA;
        private PublicKey PlotPDA;
        private bool _isInitialized;
        private LumberjackClient lumberjackClient;
        
        private void Awake() 
        {
            if (Instance != null && Instance != this) 
            { 
                Destroy(this); 
            } 
            else 
            { 
                Instance = this; 
            }

            Web3.OnLogin += OnLogin;
        }

        private void OnDestroy()
        {
            Web3.OnLogin -= OnLogin;
        }

        private async void OnLogin(Account account)
        {
            var solBalance = await Web3.Instance.WalletBase.GetBalance(Commitment.Confirmed);
            if (solBalance < 10000)
            {
                var res = await Web3.Instance.WalletBase.RequestAirdrop(commitment: Commitment.Confirmed);
                Debug.Log("airdrop result: " + res.RawRpcResponse);
            }

            PublicKey.TryFindProgramAddress(new[]
                    {Encoding.UTF8.GetBytes("player"), account.PublicKey.KeyBytes},
                LumberjackProgramIdPubKey, out PlayerDataPDA, out byte bump);
            PublicKey.TryFindProgramAddress(new[]
                    {Encoding.UTF8.GetBytes("plot"), account.PublicKey.KeyBytes},
                LumberjackProgramIdPubKey, out PlotPDA, out byte bump2);

            ServiceFactory.Resolve<SolPlayWebSocketService>().Connect("wss://rpc.helius.xyz/?api-key=dcee9dad-fb42-4a26-b394-41b53e81d913");
            ServiceFactory.Resolve<SolPlayWebSocketService>().SubscribeToPubKeyData(PlotPDA, result =>
            {
                var plot = Plot.Deserialize(Convert.FromBase64String(result.result.value.data[0]));
                OnPlotDataChanged?.Invoke(plot);
            });
            ServiceFactory.Resolve<SolPlayWebSocketService>().SubscribeToPubKeyData(PlayerDataPDA, result =>
            {
                var plot = PlayerData.Deserialize(Convert.FromBase64String(result.result.value.data[0]));
                OnPlayerDataChanged?.Invoke(plot);
            });

            lumberjackClient = new LumberjackClient(Web3.Rpc, Web3.WsRpc, LumberjackProgramIdPubKey);

            await GetAndSubscribeToAccounts();

            sessionWallet = await SessionWallet.GetSessionWallet(LumberjackProgramIdPubKey, "ingame");
            OnInitialDataLoaded?.Invoke();
        }

        private async Task GetAndSubscribeToAccounts()
        {
            AccountResultWrapper<PlayerData> playerData = null;

            try
            {
                playerData = await lumberjackClient.GetPlayerDataAsync(PlayerDataPDA, Commitment.Confirmed);
                if (playerData.ParsedResult != null)
                {
                    OnPlayerDataChanged?.Invoke(playerData.ParsedResult);
                }
            }
            catch (Exception e)
            {
                Debug.Log("Probably playerData not available " + e.Message);
            }

            if (playerData != null)
            {
                _isInitialized = true;
                Debug.Log("Player data first " + playerData.ParsedResult.Gold + " gold");
                await SubscribeToPlayerDataUpdates();
            }

            AccountResultWrapper<Plot> plotData = null;

            try
            {
                plotData = await lumberjackClient.GetPlotAsync(PlotPDA, Commitment.Confirmed);
                if (plotData.ParsedResult != null)
                {
                    OnPlotDataChanged?.Invoke(plotData.ParsedResult);
                }
            }
            catch (Exception e)
            {
                Debug.Log("Probably plot data not available " + e.Message);
            }

            if (plotData != null)
            {
                Debug.Log("Plot data currently planted " + plotData.ParsedResult.HumanType + " human type");
                await SubscribeToPlotDataUpdates();
            }
        }

        public bool IsInitialized()
        {
            return _isInitialized;
        }

        private async Task SubscribeToPlayerDataUpdates()
        {
            await lumberjackClient.SubscribePlayerDataAsync(PlayerDataPDA, OnRecievedPlayerDataUpdate, Commitment.Confirmed);
        }

        private void OnRecievedPlayerDataUpdate(SubscriptionState state, ResponseValue<AccountInfo> value, PlayerData playerData)
        {
            Debug.Log($"Socket Message state: {state.State} data: {value.Value.Data} player data {playerData}");
            Debug.Log("Player data first " + playerData.Gold + " wood");
            OnPlayerDataChanged?.Invoke(playerData);
        }

        private async Task SubscribeToPlotDataUpdates()
        {
            await lumberjackClient.SubscribePlotAsync(PlotPDA, OnRecievedPlotDataUpdate, Commitment.Confirmed);
        }

        private void OnRecievedPlotDataUpdate(SubscriptionState state, ResponseValue<AccountInfo> value, Plot playerData)
        {
            Debug.Log("Socket Message " + state + value + playerData);
            Debug.Log("Plot has a " + playerData.HumanType + " planted");
            OnPlotDataChanged?.Invoke(playerData);
        }

        public async Task<RequestResult<string>> InitGameDataAccount()
        {
            var tx = new Transaction()
            {
                FeePayer = Web3.Account,
                Instructions = new List<TransactionInstruction>(),
                RecentBlockHash = await Web3.BlockHash()
            };

            InitPlayerAccounts accounts = new InitPlayerAccounts();
            accounts.Player = PlayerDataPDA;
            accounts.Plot = PlotPDA;
            accounts.Signer = Web3.Account;
            accounts.SystemProgram = SystemProgram.ProgramIdKey;
            
            var initTx = LumberjackProgram.InitPlayer(accounts, LumberjackProgramIdPubKey);
            tx.Add(initTx);

            if (!(await sessionWallet.IsSessionTokenInitialized()))
            {
                var topUp = true;

                var validity = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds();
                var createSessionIX = sessionWallet.CreateSessionIX(topUp, validity);

                tx.Add(createSessionIX);
                Debug.Log("Has no session -> partial sign");
                tx.PartialSign(new[] { Web3.Account, sessionWallet.Account });
            }
            
            var result = await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
            if (result.WasSuccessful)
            {
                GetAndSubscribeToAccounts();
            }
            return result;
        }

        public async Task<SessionWallet> RevokeSession()
        { 
            sessionWallet.Logout();
            return sessionWallet;
        }

        public async Task<RequestResult<string>> Plant(bool useSession, string humanType)
        {
            var tx = new Transaction()
            {
                FeePayer = Web3.Account,
                Instructions = new List<TransactionInstruction>(),
                RecentBlockHash = await Web3.BlockHash(maxSeconds:3)
            };

            PlantAccounts accounts = new PlantAccounts();
            accounts.Player = PlayerDataPDA;
            accounts.Plot = PlotPDA;
            
            if (useSession)
            {
                if (!(await sessionWallet.IsSessionTokenInitialized()))
                {
                    var topUp = true;

                    var validity = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds();
                    var createSessionIX = sessionWallet.CreateSessionIX(topUp, validity);
                    accounts.Signer = Web3.Account.PublicKey;
                    tx.Add(createSessionIX);
                    var chopInstruction = LumberjackProgram.Plant(accounts, humanType, LumberjackProgramIdPubKey);
                    tx.Add(chopInstruction);
                    Debug.Log("Has no session -> partial sign");
                    tx.PartialSign(new[] { Web3.Account, sessionWallet.Account });
                }
                else
                {
                    tx.FeePayer = sessionWallet.Account.PublicKey;
                    accounts.SessionToken = sessionWallet.SessionTokenPDA;
                    accounts.Signer = sessionWallet.Account.PublicKey;
                    var chopInstruction = LumberjackProgram.Plant(accounts, humanType, LumberjackProgramIdPubKey);
                    tx.Add(chopInstruction);
                    Debug.Log("Has session -> sign and send session wallet");

                    return await sessionWallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
                }
            }
            
            return await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
        }
        
        public async Task<RequestResult<string>> Harvest(bool useSession)
        {
            var tx = new Transaction()
            {
                FeePayer = Web3.Account,
                Instructions = new List<TransactionInstruction>(),
                RecentBlockHash = await Web3.BlockHash(maxSeconds:3)
            };

            HarvestAccounts accounts = new HarvestAccounts();
            accounts.Player = PlayerDataPDA;
            accounts.Plot = PlotPDA;
            
            if (useSession)
            {
                if (!(await sessionWallet.IsSessionTokenInitialized()))
                {
                    var topUp = true;

                    var validity = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds();
                    var createSessionIX = sessionWallet.CreateSessionIX(topUp, validity);
                    accounts.Signer = Web3.Account.PublicKey;
                    tx.Add(createSessionIX);
                    var chopInstruction = LumberjackProgram.Harvest(accounts, LumberjackProgramIdPubKey);
                    tx.Add(chopInstruction);
                    Debug.Log("Has no session -> partial sign");
                    tx.PartialSign(new[] { Web3.Account, sessionWallet.Account });
                }
                else
                {
                    tx.FeePayer = sessionWallet.Account.PublicKey;
                    accounts.SessionToken = sessionWallet.SessionTokenPDA;
                    accounts.Signer = sessionWallet.Account.PublicKey;
                    var chopInstruction = LumberjackProgram.Harvest(accounts, LumberjackProgramIdPubKey);
                    tx.Add(chopInstruction);
                    Debug.Log("Has session -> sign and send session wallet");

                    return await sessionWallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
                }
            }
            
            return await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
        }
    }
