using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Solana.Unity;
using Solana.Unity.Programs.Abstract;
using Solana.Unity.Programs.Utilities;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using Lumberjack;
using Lumberjack.Program;
using Lumberjack.Errors;
using Lumberjack.Accounts;

namespace Lumberjack
{
    namespace Accounts
    {
        public partial class PlayerData
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 9264901878634267077UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{197, 65, 216, 202, 43, 139, 147, 128};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "ZzeEvyxXcpF";
            public PublicKey Authority { get; set; }

            public string Name { get; set; }

            public byte Level { get; set; }

            public ulong Xp { get; set; }

            public ulong Energy { get; set; }

            public ulong Gold { get; set; }

            public long LastLogin { get; set; }

            public static PlayerData Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                PlayerData result = new PlayerData();
                result.Authority = _data.GetPubKey(offset);
                offset += 32;
                offset += _data.GetBorshString(offset, out var resultName);
                result.Name = resultName;
                result.Level = _data.GetU8(offset);
                offset += 1;
                result.Xp = _data.GetU64(offset);
                offset += 8;
                result.Energy = _data.GetU64(offset);
                offset += 8;
                result.Gold = _data.GetU64(offset);
                offset += 8;
                result.LastLogin = _data.GetS64(offset);
                offset += 8;
                return result;
            }
        }

        public partial class Plot
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 16631235073802654291UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{83, 82, 6, 254, 46, 4, 206, 230};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "EwK83LcPSq3";
            public string HumanType { get; set; }

            public long PlantedAt { get; set; }

            public static Plot Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                Plot result = new Plot();
                offset += _data.GetBorshString(offset, out var resultHumanType);
                result.HumanType = resultHumanType;
                result.PlantedAt = _data.GetS64(offset);
                offset += 8;
                return result;
            }
        }
    }

    namespace Errors
    {
        public enum LumberjackErrorKind : uint
        {
            NotEnoughEnergy = 6000U,
            NotEnoughGold = 6001U,
            NotReadyYet = 6002U,
            NothingWasPlanted = 6003U,
            WrongAuthority = 6004U
        }
    }

    public partial class LumberjackClient : TransactionalBaseClient<LumberjackErrorKind>
    {
        public LumberjackClient(IRpcClient rpcClient, IStreamingRpcClient streamingRpcClient, PublicKey programId) : base(rpcClient, streamingRpcClient, programId)
        {
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerData>>> GetPlayerDatasAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = PlayerData.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerData>>(res);
            List<PlayerData> resultingAccounts = new List<PlayerData>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => PlayerData.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerData>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Plot>>> GetPlotsAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = Plot.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Plot>>(res);
            List<Plot> resultingAccounts = new List<Plot>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => Plot.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Plot>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<PlayerData>> GetPlayerDataAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<PlayerData>(res);
            var resultingAccount = PlayerData.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<PlayerData>(res, resultingAccount);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<Plot>> GetPlotAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<Plot>(res);
            var resultingAccount = Plot.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<Plot>(res, resultingAccount);
        }

        public async Task<SubscriptionState> SubscribePlayerDataAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, PlayerData> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                PlayerData parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = PlayerData.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<SubscriptionState> SubscribePlotAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, Plot> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                Plot parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = Plot.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<RequestResult<string>> SendInitPlayerAsync(InitPlayerAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.LumberjackProgram.InitPlayer(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendPlantAsync(PlantAccounts accounts, string humanType, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.LumberjackProgram.Plant(accounts, humanType, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendHarvestAsync(HarvestAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.LumberjackProgram.Harvest(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendUpdateAsync(UpdateAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.LumberjackProgram.Update(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        protected override Dictionary<uint, ProgramError<LumberjackErrorKind>> BuildErrorsDictionary()
        {
            return new Dictionary<uint, ProgramError<LumberjackErrorKind>>{{6000U, new ProgramError<LumberjackErrorKind>(LumberjackErrorKind.NotEnoughEnergy, "Not enough energy")}, {6001U, new ProgramError<LumberjackErrorKind>(LumberjackErrorKind.NotEnoughGold, "Not enough gold")}, {6002U, new ProgramError<LumberjackErrorKind>(LumberjackErrorKind.NotReadyYet, "Not ready for harvest yet")}, {6003U, new ProgramError<LumberjackErrorKind>(LumberjackErrorKind.NothingWasPlanted, "Nothing was planted")}, {6004U, new ProgramError<LumberjackErrorKind>(LumberjackErrorKind.WrongAuthority, "Wrong Authority")}, };
        }
    }

    namespace Program
    {
        public class InitPlayerAccounts
        {
            public PublicKey Player { get; set; }

            public PublicKey Plot { get; set; }

            public PublicKey Signer { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class PlantAccounts
        {
            public PublicKey SessionToken { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey Plot { get; set; }

            public PublicKey Signer { get; set; }
        }

        public class HarvestAccounts
        {
            public PublicKey SessionToken { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey Plot { get; set; }

            public PublicKey Signer { get; set; }
        }

        public class UpdateAccounts
        {
            public PublicKey SessionToken { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey Signer { get; set; }
        }

        public static class LumberjackProgram
        {
            public static Solana.Unity.Rpc.Models.TransactionInstruction InitPlayer(InitPlayerAccounts accounts, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Plot, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(4819994211046333298UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction Plant(PlantAccounts accounts, string humanType, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Plot, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(17770342024824781256UL, offset);
                offset += 8;
                offset += _data.WriteBorshString(humanType, offset);
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction Harvest(HarvestAccounts accounts, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Plot, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(14356254285327495652UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction Update(UpdateAccounts accounts, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(9222597562720635099UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }
        }
    }
}