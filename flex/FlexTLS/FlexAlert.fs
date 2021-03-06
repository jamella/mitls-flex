(*
 * Copyright 2015 INRIA and Microsoft Corporation
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *)

#light "off"

module FlexTLS.FlexAlert

open NLog

open Bytes
open Alert
open Error
open TLSError
open TLSConstants

open FlexTypes
open FlexConstants
open FlexState
open FlexRecord

/// <summary>
/// Module receiving, sending and forwarding Alert messages.
/// </summary>
type FlexAlert =
    class

    /// <summary>
    /// Receive an Alert message from the network stream
    /// </summary>
    /// <param name="st"> State of the current Handshake </param>
    /// <returns> Updated state * parsed alert description * alert bytes </returns>
    static member receive (st:state) : state * alertDescription * bytes =
        LogManager.GetLogger("file").Info("# ALERT : FlexAlert.receive");
        let buf = st.read.alert_buffer in
        if length buf < 2 then
            let ct,pv,len,_ = FlexRecord.parseFragmentHeader st in
            match ct with
            | Alert ->
                let st,b = FlexRecord.getFragmentContent (st, ct, len) in
                let buf = buf @| b in
                let st = FlexState.updateIncomingAlertBuffer st buf in
                FlexAlert.receive st
            | Handshake ->
                let st,b = FlexRecord.getFragmentContent (st, ct, len) in
                let buf = buf @| b in
                let st = FlexState.updateIncomingHSBuffer st buf in
                let _,hst,payload,_ = FlexHandshake.FlexHandshake.receive st in
                failwith (perror __SOURCE_FILE__ __LINE__ (sprintf "Unexpected handshake message: %A with payload %s" hst (Bytes.hexString(payload))))

            | _ ->
                let _,b = FlexRecord.getFragmentContent (st, ct, len) in
                failwith (perror __SOURCE_FILE__ __LINE__ (sprintf "Unexpected content type: %A\n Payload (%d Bytes) : %s" ct len (Bytes.hexString(b))))
        else
            let alb,rem = Bytes.split buf 2 in
            match Alert.parseAlert alb with
            | Error(ad,x) -> failwith (perror __SOURCE_FILE__ __LINE__ x)
            | Correct(ad) ->
                let st = FlexState.updateIncomingAlertBuffer st rem in
                LogManager.GetLogger("file").Info(sprintf "--- Description : %A" ad);
                LogManager.GetLogger("file").Debug(sprintf "--- Payload : %s" (Bytes.hexString(alb)));
                (st,ad,alb)

    /// <summary>
    /// Forward an Alert message received from a network stream
    /// </summary>
    /// <param name="stin"> State of the current Handshake on the incoming side </param>
    /// <param name="stout"> State of the current Handshake on the outgoing side </param>
    /// <param name="fp"> Optional fragmentation policy applied to the message </param>
    /// <returns> Updated incoming state * Updated outgoing state * forwarded alert bytes </returns>
    static member forward (stin:state, stout:state, ?fp:fragmentationPolicy) : state * state * bytes =
        LogManager.GetLogger("file").Info("# ALERT : FlexAlert.forward");
        let fp = defaultArg fp FlexConstants.defaultFragmentationPolicy in
        let stin,ad,alb = FlexAlert.receive(stin) in
        let stout   = FlexAlert.send(stout,alb,fp) in
        LogManager.GetLogger("file").Debug(sprintf "--- Payload : %s" (Bytes.hexString(alb)));
        stin,stout,alb

    /// <summary>
    /// Send an Alert message to the network stream from an Alert Description
    /// </summary>
    /// <param name="st"> State of the current Handshake </param>
    /// <param name="ad"> Alert description union type already parsed </param>
    /// <param name="fp"> Optional fragmentation policy applied to the message </param>
    /// <returns> Updated state </returns>
    static member send (st:state, ad:alertDescription, ?fp:fragmentationPolicy) : state =
        let fp = defaultArg fp FlexConstants.defaultFragmentationPolicy in
        FlexAlert.send(st, alertBytes ad, fp)

    /// <summary>
    /// Send an Alert message to the network stream
    /// </summary>
    /// <param name="st"> State of the current Handshake </param>
    /// <param name="payload"> Alert bytes </param>
    /// <param name="fp"> Optional fragmentation policy applied to the message </param>
    /// <returns> Updated state </returns>
    static member send (st:state, payload:bytes, ?fp:fragmentationPolicy) : state =
        LogManager.GetLogger("file").Info("# ALERT : FlexAlert.send");
        let fp = defaultArg fp FlexConstants.defaultFragmentationPolicy in
        let buf = st.write.alert_buffer @| payload in
        let st = FlexState.updateOutgoingAlertBuffer st buf in

        let _ =
            if length payload = 2 then
                match Alert.parseAlert payload with
                    | Error(ad,x) -> failwith (perror __SOURCE_FILE__ __LINE__ x)
                    | Correct(ad) ->
                        LogManager.GetLogger("file").Info(sprintf "--- Description : %A" ad);
            else ()
        in
        LogManager.GetLogger("file").Debug(sprintf "--- Payload : %s" (Bytes.hexString(payload)));
        FlexRecord.send(st,Alert,fp)

    end
