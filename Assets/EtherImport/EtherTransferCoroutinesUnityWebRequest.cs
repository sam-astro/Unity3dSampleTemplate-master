﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Extensions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Unity.Rpc;
using Nethereum.Unity.FeeSuggestions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;
using Nethereum.Util;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

using OPS.AntiCheat.Field;
using Nethereum.Web3;
using System.Threading.Tasks;

public class EtherTransferCoroutinesUnityWebRequest : MonoBehaviour {

    public ProtectedString Url = "http://localhost:8545";
    public ProtectedString endPayer;
    public ProtectedString PrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
    public ProtectedString AddressTo = "0xde0B295669a9FD93d5F28D9Ec85E40f4cb697BAe";
    public ProtectedDecimal Amount = 1.1m;
    public ProtectedDecimal GasPriceGwei = 35;
    public ProtectedString TransactionHash = "";
    public ProtectedDecimal BalanceAddressTo = 0m;

    public InputField InputUrl;
    public InputField InputPrivateKey;
    public InputField InputAddressTo;
    public InputField InputAmount;

    public InputField ResultBalanceAddressTo;
    public InputField ResultTxnHash;

    
    public EtherTransferCoroutinesUnityWebRequest()
    {
        
    }
    // Use this for initialization
    void Start () {
        GasPriceGwei = 35;
       
        InputUrl.text = Url;
        InputPrivateKey.text = PrivateKey;
        InputAddressTo.text = AddressTo;
        InputAmount.text = Amount.ToString();

    }

    public void TransferRequest()
    {
        StartCoroutine(TransferEther());
    }

    public enum FeeStrategy
    {
        Legacy,
        TimePreference,
        MedianFeeHistory
    }
    private Web3 _web3;
    //Sample of new features / requests
    public IEnumerator TransferEther()
    {
        Url = InputUrl.text;
        PrivateKey = InputPrivateKey.text;
        AddressTo = InputAddressTo.text;
        Amount = System.Decimal.Parse(InputAmount.text);
        _web3 = new Web3("https://mainnet.infura.io/v3/6282b01b48594c4f90c08acb9c92e169");
        var gasPriceTask = new TaskCompletionSource<BigInteger>();
        _web3.Eth.GasPrice.SendRequestAsync().ContinueWith(task => gasPriceTask.SetResult(task.Result));
        yield return new WaitUntil(() => gasPriceTask.Task.IsCompleted);
        GasPriceGwei.Equals( Web3.Convert.FromWei(gasPriceTask.Task.Result, UnitConversion.EthUnit.Gwei));
        //initialising the transaction request sender
        var ethTransfer = new EthTransferUnityRequest(Url, PrivateKey, 444444444500);

        var receivingAddress = AddressTo;

        var feeStrategy = FeeStrategy.MedianFeeHistory;

        if (feeStrategy == FeeStrategy.TimePreference)
        {
            Debug.Log("Time Preference");
            var timePreferenceFeeSuggestion = new TimePreferenceFeeSuggestionUnityRequestStrategy(Url);

            yield return timePreferenceFeeSuggestion.SuggestFees();

            if (timePreferenceFeeSuggestion.Exception != null)
            {
                Debug.Log(timePreferenceFeeSuggestion.Exception.Message);
                yield break;
            }

            //lets get the first one so it is higher priority
            Debug.Log(timePreferenceFeeSuggestion.Result.Length);
            if (timePreferenceFeeSuggestion.Result.Length > 0)
            {
                Debug.Log(timePreferenceFeeSuggestion.Result[0].MaxFeePerGas);
                Debug.Log(timePreferenceFeeSuggestion.Result[0].MaxPriorityFeePerGas);
            }
            var fee = timePreferenceFeeSuggestion.Result[0];
             
            yield return ethTransfer.TransferEther(receivingAddress, Amount, fee.MaxPriorityFeePerGas.Value, fee.MaxFeePerGas.Value);
            
           
            
            if (ethTransfer.Exception != null)
            {
                Debug.Log("Error transferring Ether using Time Preference Fee Estimation Strategy: " + ethTransfer.Exception.Message);
                yield break;
            }
        }


        if(feeStrategy == FeeStrategy.MedianFeeHistory)
        {
            Debug.Log("MedianFeeHistory mode");
            var medianPriorityFeeStrategy = new MedianPriorityFeeHistorySuggestionUnityRequestStrategy(Url);

            yield return medianPriorityFeeStrategy.SuggestFee();

            if (medianPriorityFeeStrategy.Exception != null)
            {
                Debug.Log(medianPriorityFeeStrategy.Exception.Message);
                yield break;
            }
            
            Debug.Log(medianPriorityFeeStrategy.Result.MaxFeePerGas);
            Debug.Log(medianPriorityFeeStrategy.Result.MaxPriorityFeePerGas);
            
            var fee = medianPriorityFeeStrategy.Result;


            
            
            
            
            yield return ethTransfer.TransferEther(receivingAddress, Amount, fee.MaxPriorityFeePerGas.Value, fee.MaxFeePerGas.Value);
              
          
            
            if (ethTransfer.Exception != null)
            {
                Debug.Log("Error transferring Ether using Median Fee History Fee Estimation Strategy: " + ethTransfer.Exception.Message);
                yield break;
            }
        }

        if (feeStrategy == FeeStrategy.Legacy)
        {
            Debug.Log("Legacy mode");
            //I am forcing the legacy mode but also I am including the gas price
            ethTransfer.UseLegacyAsDefault = true;

            yield return ethTransfer.TransferEther(receivingAddress, Amount, GasPriceGwei);

            if (ethTransfer.Exception != null)
            {
                Debug.Log("Error transferring Ether using Legacy Gas Price:  " + ethTransfer.Exception.Message);
                yield break;
            }

        }

        TransactionHash = ethTransfer.Result;
        ResultTxnHash.text = TransactionHash;
        Debug.Log("Transfer transaction hash:" + TransactionHash);

        //create a poll to get the receipt when mined
        var transactionReceiptPolling = new TransactionReceiptPollingRequest(Url);
        //checking every 2 seconds for the receipt
        yield return transactionReceiptPolling.PollForReceipt(TransactionHash, 2);
        
        Debug.Log("Transaction mined");

        var balanceRequest = new EthGetBalanceUnityRequest(Url);
        yield return balanceRequest.SendRequest(receivingAddress, BlockParameter.CreateLatest());

        BalanceAddressTo = UnitConversion.Convert.FromWei(balanceRequest.Result.Value);
        ResultBalanceAddressTo.text = BalanceAddressTo.ToString();

        Debug.Log("Balance of account:" + BalanceAddressTo);
    }
    
   
   

          
        
    
    
    public IEnumerator getbalanceRequest(){
         var balanceRequest = new EthGetBalanceUnityRequest(Url);
        yield return balanceRequest.SendRequest(AddressTo, BlockParameter.CreateLatest());
       
        BalanceAddressTo = UnitConversion.Convert.FromWei(balanceRequest.Result.Value);
    }
    // Update is called once per frame
    
    void Update () {
        
        
	}
    public  bool sentMoneyGood = false;


   

   
}
