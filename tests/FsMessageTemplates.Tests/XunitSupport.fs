﻿[<AutoOpen>]
module FsTests.XunitSupport

open Xunit
open System
open System.Globalization
open Swensen.Unquote

type LangTheoryAttribute() =
    inherit Xunit.TheoryAttribute()
type LangCsFsDataAttribute() =
    inherit Xunit.Sdk.DataAttribute()
    override __.GetData _ = [[|box "C#"|]; [|box "F#"|]] |> Seq.ofList

type FsToken = FsMessageTemplates.Token

let assertParsedAs lang message (expectedTokens: FsToken seq) =
    let parsed =
        match lang with
        | "C#" -> MessageTemplates.MessageTemplate.Parse(message).Tokens |> Seq.map CsToFs.mttToToken |> List.ofSeq
        | "F#" -> (FsMessageTemplates.Parser.parse message).Tokens |> List.ofSeq
        | other -> failwithf "unexpected lang '%s'" other

    let expected = expectedTokens |> Seq.cast<FsToken> |> Seq.toList
    test <@ parsed = expected @>

let capture lang (messageTemplate:string) (args: obj list) =
    let argsArray = (args |> Seq.cast<obj> |> Seq.toArray) // force 'args' to be IEnumerable
    match lang with
    | "F#" -> FsMessageTemplates.Capturing.captureMessageProperties messageTemplate argsArray |> List.ofSeq
    | "C#" -> MessageTemplates.MessageTemplate.Capture(messageTemplate, argsArray) |> Seq.map CsToFs.templateProperty |> List.ofSeq
    | other -> failwithf "unexpected lang '%s'" other

let renderp lang (provider:IFormatProvider) messageTemplate args =
    let argsArray = (args |> Seq.cast<obj> |> Seq.toArray) // force 'args' to be IEnumerable
    match lang with
    | "C#" -> MessageTemplates.MessageTemplate.Format(provider, messageTemplate, argsArray)
    | "F#" -> FsMessageTemplates.Formatting.sprintsm provider messageTemplate argsArray
    | other -> failwithf "unexpected lang '%s'" other

let render lang template args =
    renderp lang CultureInfo.InvariantCulture template args
