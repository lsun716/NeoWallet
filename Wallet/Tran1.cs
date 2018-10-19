﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using ThinNeo;

namespace Wallet {
    class Tran1 {
        public void Run() {
            string wif = "KySWX7BK1smMUVLrpp66EyzMgWeQZmkrB6FEdzBw7qhouvgaBgJd";//自己
            string targetAddress = "AN6HX6NxNsQaLdcbtqjTCP2z4XxTy1GNSr";//别人
            string asset = "0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";//币种
            decimal sendCount = new decimal(8);

            byte[] prikey = Helper.GetPrivateKeyFromWIF(wif);
            byte[] pubkey = Helper.GetPublicKeyFromPrivateKey(prikey);
            string address = Helper.GetAddressFromPublicKey(pubkey);

            SQLServer sQLServer = new SQLServer();
            sQLServer.Open();
            //接收一个读取对象
            SqlDataReader reader = sQLServer.GetUTXO(address, asset);

            //整理utxo
            Dictionary<string, List<UTXO>> dic_UTXO = GetUTXO(reader);
            sQLServer.Close();

            //拼交易
            Transaction transaction = MakeTransaction(dic_UTXO, address, targetAddress, new Hash256(asset), sendCount);


        }

        private Transaction MakeTransaction(Dictionary<string, List<UTXO>> dic_UTXO, string fromAddress, string targetAddress, Hash256 asset, decimal sendCount) {
            //从字典取出utxo列表
            List<UTXO> uTXOs = dic_UTXO[asset.ToString()];

            Transaction transaction = new Transaction();

            decimal count = 0;
            List<TransactionInput> transactionInputs = new List<TransactionInput>();
            for(int i = 0; i < uTXOs.Count; i++) {
                TransactionInput transactionInput = new TransactionInput();
                transactionInput.hash = uTXOs[i].txid;
                transactionInput.index = (ushort)uTXOs[i].n;

                transactionInputs.Add(transactionInput);
                count += uTXOs[i].value;
                if(count >= sendCount) {
                    break;
                }
            }

            transaction.inputs = transactionInputs.ToArray();

            //输入大于等于输出
            if(count >= sendCount) {
                List<TransactionOutput> transactionOutputs = new List<TransactionOutput>();
                //输出
                if(sendCount > 0) {
                    TransactionOutput transactionOutput = new TransactionOutput();
                    transactionOutput.assetId = asset;
                    transactionOutput.value = sendCount;
                    transactionOutput.toAddress = Helper.GetPublicKeyHashFromAddress(targetAddress);
                    transactionOutputs.Add(transactionOutput);
                }

                //找零
                decimal change = count - sendCount;
                if(change > 0) {
                    TransactionOutput transactionOutput = new TransactionOutput();
                    transactionOutput.toAddress = Helper.GetPublicKeyHashFromAddress(fromAddress);
                    transactionOutput.assetId = asset;
                    transactionOutputs.Add(transactionOutput);
                }
                transaction.outputs = transactionOutputs.ToArray();
            } else {
                throw new Exception("余额不足!");
            }
            return transaction;
        }

        private Dictionary<string, List<UTXO>> GetUTXO(SqlDataReader reader) {
            //建一个以asset为key,utxo对象为value的字典
            Dictionary<string, List<UTXO>> dic = new Dictionary<string, List<UTXO>>();

            //读取reader并写入字典
            while(reader.Read()) {
                Hash256 txid = new Hash256(reader["txid"].ToString());
                int n = int.Parse(reader["n"].ToString());
                string asset = reader["asset"].ToString();
                string address = reader["address"].ToString();
                decimal value = decimal.Parse(reader["value"].ToString());

                UTXO uTXO = new UTXO(txid, n, asset, address, value);

                if(dic.ContainsKey(asset)) {
                    dic[asset].Add(uTXO);
                } else {
                    List<UTXO> uTXOs = new List<UTXO>();
                    uTXOs.Add(uTXO);
                    dic[asset] = uTXOs;
                }
            }
            if(!dic.ContainsKey(reader["asset"].ToString())) {
                throw new Exception("你都没有这种钱");//须添加币种显示
            }
            return dic;
        }
    }
}