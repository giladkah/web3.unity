﻿using System;

namespace Web3Unity.Scripts.Library.Ethers.Providers
{
    [Serializable]
    public class JsonRpcProviderConfig
    {
        /// <summary>
        /// (Optional) Url of RPC Node.
        /// </summary>
        public string RpcNodeUrl { get; set; }
    }
}