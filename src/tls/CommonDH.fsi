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

module CommonDH

open Bytes
open Error
open TLSConstants
open CoreKeys

type element = {
 dhe_p : DHGroup.elt option;
 dhe_ec : ECGroup.point option;
}
val dhe_nil : element

type secret = Key of bytes

type parameters =
| DHP_P of dhparams
| DHP_EC of ecdhparams

// exception Invalid_DH

val leak:   parameters -> element -> secret -> bytes
val coerce: parameters -> element -> bytes -> secret

val get_p: element -> DHGroup.elt
#if verify
#else
val get_ec: element -> ECGroup.point
#endif

// (p, g, g^x) payload of ServerKeyExchange for (EC)DH, additionally signed/verified for (EC)DHE
val serializeKX: parameters -> element -> bytes

val checkParams: DHDB.dhdb option -> int * int -> parameters -> (DHDB.dhdb option * parameters) TLSError.Result

val parse: parameters -> bytes -> element option

val checkElement: parameters -> element -> element option
