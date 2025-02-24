using System;
using System.Numerics;
using System.Threading.Tasks;
using ChainSafe.GamingSdk.Gelato.Types;
using ChainSafe.GamingWeb3;
using Newtonsoft.Json;
using Web3Unity.Scripts.Library.Ethers.Contracts;
using Web3Unity.Scripts.Library.Ethers.Providers;
using Contract = Web3Unity.Scripts.Library.Ethers.Contracts.Contract;

namespace ChainSafe.GamingSdk.Gelato.Dto
{
    public enum Erc2771Type
    {
        /// <summary>
        /// For requesting a relayed transaction where the contract funds the transaction
        /// </summary>
        CallWithSyncFee,

        /// <summary>
        /// For sponsoring the transaction from the developers account
        /// </summary>
        SponsoredCall,
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]

    public class CallWithErc2771Request
    {
        /// <summary>
        ///    QUANTITY - The transaction chain id.
        /// </summary>
        [JsonProperty(PropertyName = "chainId")]
        public int ChainId { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - The address the transaction is being sent to.
        /// </summary>
        [JsonProperty(PropertyName = "target")]
        public string Target { get; set; }

        /// <summary>
        ///     DATA - the data send along with the transaction.
        /// </summary>
        [JsonProperty(PropertyName = "data")]
        public string Data { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - the address of the token that is to be used for payment.
        /// </summary>
        [JsonProperty(PropertyName = "feeToken")]
        public string FeeToken { get; set; }

        /// <summary>
        ///     DATA - an optional boolean (default: true ) denoting what data you would prefer appended to the end of the calldata.
        /// </summary>
        [JsonProperty(PropertyName = "isRelayContext")]
        public bool IsRelayContext { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - the address of the user's EOA.
        /// </summary>
        [JsonProperty(PropertyName = "user")]
        public string User { get; set; }

        /// <summary>
        ///    QUANTITY - optional, this is a nonce similar to Ethereum nonces, stored in a local mapping on the relay contracts. It is used to enforce nonce ordering of relay calls, if the user requires it. Otherwise, this is an optional parameter and if not passed, the relay-SDK will automatically query on-chain for the current value.
        /// </summary>
        [JsonProperty(PropertyName = "userNonce")]
        public int? UserNonce { get; set; }

        /// <summary>
        ///    QUANTITY - optional, the amount of time in seconds that a user is willing for the relay call to be active in the relay backend before it is dismissed.
        /// </summary>
        [JsonProperty(PropertyName = "userDeadline")]
        public int? UserDeadline { get; set; }

        /// <summary>
        ///    DATA - the signature from the sign typed data request.
        /// </summary>
        [JsonProperty(PropertyName = "userSignature")]
        public string Signature { get; set; }

        /// <summary>
        ///    DATA - the signature from the sign typed data request.
        /// </summary>
        [JsonProperty(PropertyName = "sponsorApiKey")]
        public string SponsorApiKey { get; set; }

        public TStructType MapRequestToStruct<TStructType>(
            CallWithErc2771RequestOptionalParameters overrides,
            Erc2771Type type)
            where TStructType : IErc2771StructTypes, new()
        {
            if (overrides.UserNonce == null && UserNonce == null)
            {
                throw new Web3Exception("UserNonce is not found in the request, nor fetched");
            }

            if (overrides.UserDeadline == null && UserDeadline == null)
            {
                throw new Web3Exception("UserDeadline is not found in the request, nor fetched");
            }

            var newStruct = (CallWithErc2771Request)MemberwiseClone();

            newStruct.UserNonce = overrides.UserNonce ?? UserNonce;
            newStruct.UserDeadline = overrides.UserDeadline ?? UserDeadline;

            var formattedStruct = (TStructType)Activator.CreateInstance(typeof(TStructType));
            formattedStruct.ChainId = newStruct.ChainId.ToString();
            formattedStruct.Target = newStruct.Target;
            formattedStruct.Data = newStruct.Data;
            formattedStruct.User = newStruct.User;
            formattedStruct.UserNonce = newStruct.UserNonce.ToString();
            formattedStruct.UserDeadline = newStruct.UserDeadline.ToString();

            return formattedStruct;
        }
    }

    public class CallWithErc2771RequestOptionalParameters
    {
        /// <summary>
        ///    QUANTITY - optional, this is a nonce similar to Ethereum nonces, stored in a local mapping on the relay contracts. It is used to enforce nonce ordering of relay calls, if the user requires it. Otherwise, this is an optional parameter and if not passed, the relay-SDK will automatically query on-chain for the current value.
        /// </summary>
        [JsonProperty(PropertyName = "userNonce")]
        public int? UserNonce { get; set; }

        /// <summary>
        ///    QUANTITY - optional, the amount of time in seconds that a user is willing for the relay call to be active in the relay backend before it is dismissed.
        /// </summary>
        [JsonProperty(PropertyName = "userDeadline")]
        public int? UserDeadline { get; set; }

        public static async Task<CallWithErc2771RequestOptionalParameters> PopulateOptionalUserParameters(
            CallWithErc2771Request request,
            Erc2771Type type,
            GelatoConfig config,
            IChainConfig chainConfig,
            IContractBuilder contractBuilder)
        {
            var optionalParams = new CallWithErc2771RequestOptionalParameters();
            if (request.UserDeadline == null)
            {
                optionalParams.UserDeadline = CalculateDeadline();
            }

            if (request.UserNonce == null)
            {
                // Must be custom nonce from the relay contract
                optionalParams.UserNonce = await GetUserNonce(request.User, type, config, chainConfig, contractBuilder);
            }

            return optionalParams;
        }

        public static string GetGelatoRelayErc2771Address(
            Erc2771Type type,
            GelatoConfig config,
            IChainConfig chainConfig)
        {
            return type switch
            {
                Erc2771Type.CallWithSyncFee => IsZkSync(chainConfig.ChainId)
                    ? config.GelatoRelayErc2771ZkSyncAddress
                    : config.GelatoRelayErc2771Address,
                Erc2771Type.SponsoredCall => IsZkSync(chainConfig.ChainId)
                    ? config.GelatoRelay1BalanceErc2771ZkSyncAddress
                    : config.GelatoRelay1BalanceErc2771Address,
                _ => throw new Web3Exception("incorrect relay option")
            };
        }

        private static bool IsZkSync(string chainId)
        {
            return chainId is "324" or "280";
        }

        private static async Task<int> GetUserNonce(
            string account,
            Erc2771Type type,
            GelatoConfig config,
            IChainConfig chainConfig,
            IContractBuilder contractBuilder)
        {
            var contract = contractBuilder.Build(
                GelatoAbi.UserNonce,
                GetGelatoRelayErc2771Address(type, config, chainConfig));
            var result = await contract.Call("userNonce", new object[] { account });

            return int.Parse(((BigInteger)result[0]).ToString());
        }

        private static int CalculateDeadline()
        {
            var parsed =
                ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds() + GelatoClient.DefaultDeadlineGap;
            return (int)parsed;
        }
    }
}