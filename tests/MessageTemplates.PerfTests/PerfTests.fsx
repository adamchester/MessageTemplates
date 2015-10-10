﻿#r "System.IO"
#r "System.Runtime"
#r "System"

#I "bin/Release/"
#r "FsMessageTemplates"
#r "MessageTemplates"
#r "MessageTemplates.PerfTests"

type PropFormat = {
    Format: string
    Hint: FsMessageTemplates.DestrHint
    Align: FsMessageTemplates.AlignInfo option }
    with
        member x.ToNamedFormatString (name) =
            let align = if x.Align.IsSome then x.Align.Value else FsMessageTemplates.AlignInfo.Empty
            let pt = FsMessageTemplates.PropertyToken(name, -1, x.Hint, align, x.Format)
            pt.ToString()

        static member Empty = { Format=""; Hint=FsMessageTemplates.DestrHint.Default; Align=None }
        static member LitStr = { PropFormat.Empty with Format="l" }
        static member Destructure = { PropFormat.Empty with Hint=FsMessageTemplates.DestrHint.Destructure }

/// Describes the tested string templates in a generic-enough way
type TestTemplateToken =
    | Literal of raw:string
    | Prop of name:string * value:obj * format:PropFormat

type TestTemplate =
    { Title: string
      /// The messge template parts and values
      TokenValues: TestTemplateToken list
      /// A pre-built dictionary of names and values, for those implementations which
      /// require dictionary input.
      DictionaryOfNamesAndValues: System.Collections.Generic.IDictionary<string, obj>
      /// An object with public properties and values; for those implementations which require
      /// an object as input.
      ObjectWithNamedProps: obj
      /// The expected rendered result
      ExpectedOutput: string }

module TestCases =
    type OneStringNamed_p1 = { p1: string }
    let named1string =
        { Title = "named 1x string"
          TokenValues = [Literal "you have priority "; Prop("p1", "one", PropFormat.LitStr); Literal "\n"]
          DictionaryOfNamesAndValues = dict ["p1", box "one"]
          ObjectWithNamedProps = { p1="one" }
          ExpectedOutput = "you have priority one\n" }

    type NameAndManager = { name: string; manager: string }
    let named2string =
        { Title = "named 2x string"
          TokenValues = [ Literal "hello, "; Prop("name", "adam", PropFormat.LitStr)
                          Literal ", your manager is "; Prop("manager", "john", PropFormat.LitStr)
                          Literal "\n" ]
          DictionaryOfNamesAndValues = dict ["name", box "adam"; "manager", box "john"]
          ObjectWithNamedProps = { name="adam"; manager="john" }
          ExpectedOutput = "hello, adam, your manager is john\n" }

module DestructuringTestCases =
    type Person = { Name: string; Manager: string }
    let samePosRepDestPerson = { Name="Adam"; Manager="john" }
    let samePosRepDestArg0 = Prop("0", samePosRepDestPerson, { PropFormat.Destructure with Format="l" })
    let repeatCount = 7
    let samePosRepDestr =
        { Title = "same pos " + string repeatCount + " destr"
          TokenValues = List.replicate repeatCount samePosRepDestArg0
          DictionaryOfNamesAndValues = dict [ "0", box samePosRepDestArg0 ]
          ObjectWithNamedProps = null
          ExpectedOutput = String.replicate repeatCount "Person { Name: \"Adam\", Manager: \"john\" }" }

module AlignmentTestCases =
    let fmtLitStringNoQuotes = "l"
    let posIsmAr10Values = [10..20]
    let posIsmAr10Toks = posIsmAr10Values |> List.mapi (fun idx num -> Prop(string idx, num, PropFormat.Empty))
    let posIsmAr10 =
        { Title = "pos int*str*decimal AR10"
          TokenValues = [Literal "hello, "] @ posIsmAr10Toks
          DictionaryOfNamesAndValues = dict [ "0", box 9 ]
          ObjectWithNamedProps = null // not supported for this test
          ExpectedOutput = "hello, " }

module Features =
    /// Allows a tested implementation to configure it's output with the provided TextWriter
    type OutputInitialiser<'OutputSink> = System.IO.TextWriter -> 'OutputSink

    /// Takes our common test template as input and produces a template usable by the tested implementation.
    type ParseInputCreator<'ImplementationParseInput> = TestTemplate -> 'ImplementationParseInput

    /// Takes our common test template as input and produces arguments that can be formatted by the
    /// tested implementation.
    type TemplateArgsCreator<'ImplementationArgs> = TestTemplate -> 'ImplementationArgs

    /// Accepts the template and arguments/values as input, then writes the formatted output. All tested
    /// implementations must be capable of this feature.
    type ParseCaptureFormat<'ImplementationParseInput, 'FormatArgs, 'OutputSink> =
        'ImplementationParseInput -> 'FormatArgs -> 'OutputSink -> unit

    /// Accepts the template as input and produces a parsed or pre-processed version of it. Not all tested implementations
    /// are capable of this feature.
    type TemplateParser<'ImplementationParseInput, 'ParsedTemplate> = 'ImplementationParseInput -> 'ParsedTemplate

    /// Takes a parsed template and values and produces an implementation-specific structure that contains
    /// the captured properties. Not all tested implementations are capable of this feature.
    type Capturer<'ParsedTemplate, 'FormatArgs, 'CapturedProperties> =
        'ParsedTemplate -> 'FormatArgs -> 'CapturedProperties

    /// Takes a parsed template and values then formats and renders the output. Not all tested implementations are
    /// capable of this feature.
    type CaptureFormatter<'ParsedTemplate, 'FormatArgs, 'OutputSink> =
        'ParsedTemplate -> 'FormatArgs -> 'OutputSink -> unit

    /// Takes a parsed template and pre-captured values and renders them to the output. Not all tested implementations
    /// are capable of this feature.
    type Formatter<'ParsedTemplate, 'CapturedProperties, 'OutputSink> =
        'ParsedTemplate -> 'CapturedProperties -> 'OutputSink -> unit

#I "../../packages/PerfUtil/lib/net40"
#r "PerfUtil"

#I "../../packages/Antlr4.StringTemplate/lib/net35"
#r "Antlr4.StringTemplate"

#I "../../packages/Serilog/lib/net45"
#r "Serilog"
#r "Serilog.FullNetFx"

#I "../../packages/NamedFormat/lib"
#r "NamedFormat"

#I "../../packages/log4net/lib/net40-full"
#r "log4net"

#I "../../packages/DotLiquid/lib/net45"
#r "DotLiquid"

open PerfUtil

module Implementations =
    open System.Collections.Generic

    /// Describes an implementation of the various features we are testing. All testables must
    /// have at least a name, and be capable of implementing the ParseCaptureFormat<'OutputSink>'
    /// style which takes a string template and the values as input, then writes the formatted
    /// output to the text writer.
    type Implementation<'ParseInput, 'FormatArgs, 'OutputSink, 'ParsedTemplate, 'CapturedProperties> =
        { Name:                 string
          /// Allows the testable implementation to create and configure some kind of object that
          /// that will use the provided TextWriter as output.
          initOutput:           Features.OutputInitialiser<'OutputSink>
          /// Allows the testable implementation to convert the template into a format suitable
          /// for the implementation to use.
          initTemplate:         Features.ParseInputCreator<'ParseInput>
          /// Given the common test template arguments, converts them to a format
          /// suitable for use as the input to the tested implementation.
          initTemplateArgs:     Features.TemplateArgsCreator<'FormatArgs>
          /// Required. Takes the parsed template as input, the
          parseCaptureFormat:   Features.ParseCaptureFormat<'ParseInput, 'FormatArgs, 'OutputSink>

          parse:                Features.TemplateParser<'ParseInput, 'ParsedTemplate> option
          capture:              Features.Capturer<'ParsedTemplate, 'FormatArgs, 'CapturedProperties> option
          captureFormat:        Features.CaptureFormatter<'ParsedTemplate, 'FormatArgs, 'OutputSink> option
          format:               Features.Formatter<'ParsedTemplate, 'CapturedProperties, 'OutputSink> option }

    let private initUsingTextWriter : Features.OutputInitialiser<System.IO.TextWriter> = fun tw -> tw

    let fmtOrEmpty fmt = if fmt = "" then "" else ":" + fmt

    /// Creates a named format template string such as "Hello, {name:l}" from the common test templates.
    let private namedFormatStringTemplateCreator : Features.ParseInputCreator<string> =
        fun tt ->
            tt.TokenValues
            |> List.map (function
                | Literal raw -> raw
                | Prop (name,_,fmt) -> fmt.ToNamedFormatString (name)
            )
            |> String.concat ""
    
    /// Creates a named format template string such as "Hello, {name}" from the common test templates.
    let private namedFormatStringNoFmtTemplateCreator : Features.ParseInputCreator<string> =
        fun tt ->
            tt.TokenValues
            |> List.map (function
                | Literal raw -> raw
                | Prop (name,_,_) -> "{" + name + "}")
            |> String.concat ""
    
    /// Creates a named format template string such as "Hello, {{name}}" from the common test templates.
    let private doubeBraceNamedNoFmtTemplateCreator : Features.ParseInputCreator<string> =
        fun tt ->
            tt.TokenValues
            |> List.map (function
                | Literal raw -> raw
                | Prop (name,_,_) -> "{{" + name + "}}")
            |> String.concat ""

    /// Creates a positional format template string such as "Hello, {0}" from the common test templates.
    let private positionalFormatStringTemplateCreator : Features.ParseInputCreator<string> =
        fun tt ->
            let mutable pos = 0
            let nextPos() = let beforeIncr = pos in pos<-pos+1; beforeIncr
            tt.TokenValues
            |> List.map (function
                | Literal raw -> raw
                | Prop (_,_,fmt) -> fmt.ToNamedFormatString (string (nextPos())))
            |> String.concat ""

    /// Given a TestTemplate as input, this creates an obj[] of property values for input to
    /// the format implementations.
    let private positionalFormatArgsArrayCreator : Features.TemplateArgsCreator<obj[]> =
        fun tt -> tt.TokenValues
                  |> List.choose (function
                    | Prop (_,v,_) -> Some v
                    | _ -> None) |> Array.ofList

    let private fsPickValueByName (pnvs: FsMessageTemplates.PropertyNameAndValue seq) name =
        pnvs |> Seq.pick (fun pnv -> if pnv.Name = name then Some (pnv.Value) else None)

    let private nameValueHashtableArgsCreator : Features.TemplateArgsCreator<System.Collections.Hashtable> =
        fun tt ->
            let ht = System.Collections.Hashtable()
            tt.TokenValues
            |> List.choose (function Prop (n,v,_) -> Some (n, v) | _ -> None)
            |> List.iter (fun (n,v) -> ht.Add(n, v))
            ht

    let fsMessageTemplates = {
        Name                = "F# MessageTemplates"
        initOutput          = initUsingTextWriter
        initTemplate        = namedFormatStringTemplateCreator
        initTemplateArgs    = positionalFormatArgsArrayCreator
        parseCaptureFormat  = fun mt vals tw -> FsMessageTemplates.Formatting.fprintsm tw mt vals
        parse               = Some <| fun mt -> FsMessageTemplates.Parser.parse mt
        capture             = Some <| fun tm args -> FsMessageTemplates.Capturing.captureProperties tm args
        captureFormat       = Some <| fun tm args tw -> FsMessageTemplates.Formatting.fprintm tm tw args
        format              = Some <| fun tm pnvs tw -> FsMessageTemplates.Formatting.formatCustom tm tw (fsPickValueByName pnvs) }


    open System.Linq // For enumerable.ToDictionary()

    let csMessageTemplates = {
        Name                = "C# MessageTemplates"
        initOutput          = initUsingTextWriter
        initTemplate        = namedFormatStringTemplateCreator
        initTemplateArgs    = positionalFormatArgsArrayCreator
        parseCaptureFormat  = fun mt vals tw -> MessageTemplates.MessageTemplate.Format(tw.FormatProvider, tw, mt, vals)
        parse               = Some <| fun mt -> MessageTemplates.MessageTemplate.Parse(mt)
        capture             = Some <| fun tm args -> MessageTemplates.MessageTemplate.Capture(tm, args)
        captureFormat       = Some <| fun tm args tw -> tm.Format(tw.FormatProvider, tw, args)
        format              = Some <| fun tm tps tw ->
                                        // TODO: maybe implement IReadOnlyDictionary<TKey, TValue> over array?
                                        let dict = tps.ToDictionary((fun tp -> tp.Name),
                                                                    elementSelector=(fun tp -> tp.Value))
                                        tm.Render(dict, tw, tw.FormatProvider) }

    open Serilog

    let serilog = {
        Name                = "Serilog"
        initOutput          = fun tw -> Serilog.LoggerConfiguration().WriteTo.TextWriter(tw, outputTemplate="{Message}").CreateLogger()
        initTemplate        = namedFormatStringTemplateCreator
        initTemplateArgs    = positionalFormatArgsArrayCreator
        parseCaptureFormat  = fun mt vals logger -> logger.Information(mt, vals)
        parse               = Some <| fun mt -> Serilog.Parsing.MessageTemplateParser().Parse(mt)
        capture             = Some <| fun tm args ->
                                        // TOOD: is this the most similar to to capture values ??
                                        let mutable logger : Serilog.ILogger = null
                                        for i = 0 to args.Length - 1 do
                                            logger <- Serilog.Log.Logger.ForContext(string i, args.[i], destructureObjects=true)
                                        logger
        captureFormat       = Some <| fun tm args logger -> logger.Information(tm.Text, args)
        format              = Some <| fun tm withCaptured _ -> withCaptured.Information(tm.Text)
    }

    let dotLiquid = {
        Name                = "DotLiquid"
        initOutput          = initUsingTextWriter
        initTemplate        = doubeBraceNamedNoFmtTemplateCreator
        initTemplateArgs    = fun tt -> tt.DictionaryOfNamesAndValues
        parseCaptureFormat  = fun mt args tw ->
                                let tmpl = DotLiquid.Template.Parse mt
                                let context = DotLiquid.Context()
                                context.Merge(DotLiquid.Hash.FromDictionary(args))
                                tmpl.Render(tw, DotLiquid.RenderParameters.FromContext(context))
        parse               = Some <| fun mt -> DotLiquid.Template.Parse mt
        capture             = Some <| fun mt args ->
                                let context = DotLiquid.Context()
                                context.Merge(DotLiquid.Hash.FromDictionary(args))
                                DotLiquid.RenderParameters.FromContext(context)
        captureFormat       = Some <| fun tm args tw ->
                                let context = DotLiquid.Context()
                                context.Merge(DotLiquid.Hash.FromDictionary(args))
                                tm.Render(tw, DotLiquid.RenderParameters.FromContext(context))
        format              = Some <| fun tm rparams tw -> tm.Render(tw, rparams)
    }
    open MessageTemplates.PerfTests.Logging

    let liblogSerilog = {
        Name                = "LibLog->Serilog"
        initOutput          = fun tw ->
                                Serilog.Log.Logger <- Serilog.LoggerConfiguration()
                                    .WriteTo.TextWriter(tw, outputTemplate="{Message}")
                                    .CreateLogger()
                                let serilogProvider = Helper.ResolveLogProvider(Helper.LibLogKind.Serilog)
                                LogProvider.SetCurrentLogProvider serilogProvider
                                LogProvider.GetLogger "LibLog->Serilog PerfTests"
        initTemplate        = namedFormatStringTemplateCreator
        initTemplateArgs    = positionalFormatArgsArrayCreator
        parseCaptureFormat  = fun mt vals logger -> logger.InfoFormat (mt, vals)
        parse               = None // no way to pre-parse this
        capture             = None // no way to parse or capture property names
        captureFormat       = None // no way to parse or capture property names
        format              = None // no way to parse or capture property names
    }


    let liblogLog4net = {
        Name                = "LibLog->Log4Net"
        initOutput          = fun tw ->
                                let twa = log4net.Appender.TextWriterAppender(
                                            Writer=tw, Layout=log4net.Layout.PatternLayout("%m"))
                                twa.ActivateOptions()
                                log4net.Config.BasicConfigurator.Configure(twa) |> ignore
                                let log4netProvider = Helper.ResolveLogProvider(Helper.LibLogKind.Log4Net)
                                LogProvider.SetCurrentLogProvider log4netProvider
                                LogProvider.GetLogger "LibLogLog4netPerfTests"
        initTemplate        = namedFormatStringNoFmtTemplateCreator
        initTemplateArgs    = positionalFormatArgsArrayCreator
        parseCaptureFormat  = fun mt vals logger -> logger.InfoFormat (mt, vals)
        parse               = None // no way to pre-parse this
        capture             = None // no way to parse or capture property names
        captureFormat       = None // no way to parse or capture property names
        format              = None // no way to parse or capture property names
    }

    let ioTextWriter = {
        Name                = "TextWriter"
        initOutput          = initUsingTextWriter
        initTemplate        = positionalFormatStringTemplateCreator
        initTemplateArgs    = positionalFormatArgsArrayCreator
        parseCaptureFormat  = fun mt vals tw -> tw.Write(mt, vals)
        parse               = None // no way to pre-parse this
        capture             = None // no way to parse or capture property names
        captureFormat       = None // no way to parse or capture property names
        format              = None // no way to parse or capture property names
    }

    let ioStringBuilder = {
        Name                = "StringBuilder"
        initOutput          = fun tw -> tw, System.Text.StringBuilder()
        initTemplate        = positionalFormatStringTemplateCreator
        initTemplateArgs    = positionalFormatArgsArrayCreator
        parseCaptureFormat  = fun mt vals (tw, sb) ->
                                tw.Write(sb.Clear().AppendFormat(mt, vals).ToString())
        parse               = None // no way to pre-parse this
        capture             = None // no way to parse or capture property names
        captureFormat       = None // no way to parse or capture property names
        format              = None // no way to parse or capture property names
    }

    let stringInject = {
        Name                = "StringInject"
        initOutput          = initUsingTextWriter
        initTemplate        = namedFormatStringNoFmtTemplateCreator
        initTemplateArgs    = nameValueHashtableArgsCreator
        parseCaptureFormat  = fun mt vals tw -> tw.Write(StringInject.StringInjectExtension.Inject(mt, vals))
        parse               = None // no way to pre-parse this
        capture             = None // no way to parse or capture property names
        captureFormat       = None // no way to parse or capture property names
        format              = None // no way to parse or capture property names
    }

    open NamedFormat

    let private createNamedImpl name (formatter: string->obj->string) : Implementation<string, obj, System.IO.TextWriter, unit, unit> =
        { Name                = name
          initOutput          = initUsingTextWriter
          initTemplate        = namedFormatStringNoFmtTemplateCreator
          initTemplateArgs    = fun tt -> tt.ObjectWithNamedProps
          parseCaptureFormat  = fun mt args tw ->
                                    let result = formatter mt args
                                    tw.Write(result)
          parse               = None // no way to pre-parse this
          capture             = None // no way to parse or capture property names
          captureFormat       = None // no way to parse or capture property names
          format              = None // no way to parse or capture property names
        }

    let haackFormat     = createNamedImpl "HaackFormat"     (fun mt args -> mt.HaackFormat(args))
    let hanselmanFormat = createNamedImpl "HanselmanFormat" (fun mt args -> mt.HanselmanFormat(args))
    let henriFormat     = createNamedImpl "HenriFormat"     (fun mt args -> mt.HenriFormat(args))
    let jamesFormat     = createNamedImpl "JamesFormat"     (fun mt args -> mt.JamesFormat(args))
    let oskarFormat     = createNamedImpl "OskarFormat"     (fun mt args -> mt.OskarFormat(args))

    // Hey this is getting too tricky, we don't know the type structure of the templates dynamically
//    let objToPrintfSpecifier = function
//        | Literal raw -> raw
//        | NamedProperty (_, v) ->
//            match v with
//            | :? string  -> "%s"
//            | :? int -> "%i"
//            | :? bool -> "%b"
//            | :? char -> "%c"
//            | :? decimal -> "%M"
//            | _ -> "%O"
//
//    let fsprintf = {
//        Name                = "F# fprintf"
//        initOutput          = initUsingTextWriter
//        initTemplate        = fun tt ->
//                                let templateString = tt |> Seq.map objToPrintfSpecifier |> String.concat ""
//                                Printf.TextWriterFormat<unit>(templateString)
//        initTemplateArgs    = fun tt ->
//                                let args = fun p1 -> ()
//                                args
//        parseCaptureFormat  = fun mt vals tw -> Printf.fprintf tw mt (obj())
//        parse               = Some <| fun mt -> Printf.TextWriterFormat<obj[]>(mt)
//        capture             = None // no way to parse or capture property names
//        captureFormat       = None // no way to parse or capture property names
//        format              = None // no way to parse or capture property names
//    }

type IFormatTest =
    inherit ITestable
    abstract DoIt : unit -> unit
    abstract Output : System.IO.TextWriter

let createParseCaptureFormatTest testTemplate tw (impl: Implementations.Implementation<_,_,_,_,_>) =

    printfn "%s: initialising with '%s'" impl.Name testTemplate.Title

    let template = impl.initTemplate testTemplate
    printfn "%s: template = '%s'" impl.Name ((string template).Replace("\n", "\\n"))

    let args = impl.initTemplateArgs testTemplate
    printfn "%s: args = '%A'" impl.Name args

    let state = impl.initOutput tw

    printfn "%s: ensuring output is expected '%s'" impl.Name (testTemplate.ExpectedOutput.Replace("\n", "\\n"))
    let actualOutput = impl.parseCaptureFormat template args state; tw.ToString()
    if actualOutput <> testTemplate.ExpectedOutput then
        failwithf "Test '%s', Impl '%s':\nExpected: %s\nActual: %s" testTemplate.Title impl.Name testTemplate.ExpectedOutput actualOutput

    { new IFormatTest with
        member __.Name = impl.Name
        member __.DoIt () = impl.parseCaptureFormat template args state
        member __.Fini () = ()
        member __.Init () = ()
        member __.Output = tw }

let createCaptureFormatTest testTemplate tw (impl: Implementations.Implementation<_,_,_,_,_>) =

    printfn "%s: initialising with '%s'" impl.Name testTemplate.Title

    let template = impl.parse.Value (impl.initTemplate testTemplate)

    let args = impl.initTemplateArgs testTemplate
    printfn "%s: args = '%A'" impl.Name args

    let state = impl.initOutput tw

    printfn "%s: ensuring output is expected '%s'" impl.Name (testTemplate.ExpectedOutput.Replace("\n", "\\n"))
    let actualOutput = impl.captureFormat.Value template args state; tw.ToString()
    if actualOutput <> testTemplate.ExpectedOutput then
        failwithf "Test '%s', Impl '%s':\nExpected: %s\nActual: %s" testTemplate.Title impl.Name testTemplate.ExpectedOutput actualOutput

    { new IFormatTest with
        member __.Name = impl.Name
        member __.DoIt () = impl.captureFormat.Value template args state
        member __.Fini () = ()
        member __.Init () = ()
        member __.Output = tw }

open Implementations
let newTw () = new System.IO.StringWriter() :> System.IO.TextWriter

let createAllNamedOrPosComparer tt =
    let theOne = createCaptureFormatTest tt (newTw()) fsMessageTemplates
    let theOthers = [
        createCaptureFormatTest tt (newTw()) csMessageTemplates
        createCaptureFormatTest tt (newTw()) dotLiquid
        createParseCaptureFormatTest tt (newTw()) serilog
        createParseCaptureFormatTest tt (newTw()) liblogSerilog
        createParseCaptureFormatTest tt (newTw()) liblogLog4net
        createParseCaptureFormatTest tt (newTw()) ioStringBuilder
        createParseCaptureFormatTest tt (newTw()) ioTextWriter
        createParseCaptureFormatTest tt (newTw()) stringInject
        createParseCaptureFormatTest tt (newTw()) haackFormat
        createParseCaptureFormatTest tt (newTw()) hanselmanFormat
        createParseCaptureFormatTest tt (newTw()) henriFormat
        createParseCaptureFormatTest tt (newTw()) jamesFormat
        createParseCaptureFormatTest tt (newTw()) oskarFormat
    ]
    tt, ImplementationComparer(theOne, theOthers, warmup=true, verbose=true, throwOnError=false)

let createAllDestructuringComparer tt =
    let theOne = createCaptureFormatTest tt (newTw()) fsMessageTemplates
    let theOthers = [
        createCaptureFormatTest tt (newTw()) csMessageTemplates
        createParseCaptureFormatTest tt (newTw()) serilog
        createParseCaptureFormatTest tt (newTw()) liblogSerilog
    ]
    tt, ImplementationComparer(theOne, theOthers, warmup=true, verbose=true, throwOnError=false)

let comparers = [
    createAllNamedOrPosComparer TestCases.named1string
    createAllNamedOrPosComparer TestCases.named2string
    createAllDestructuringComparer DestructuringTestCases.samePosRepDestr ]

comparers |> List.iter (fun (tt, c) -> c.Run(id="10k x " + tt.Title, repeat=10000, testF=fun t -> t.DoIt()))

#load "../../packages/FSharp.Charting/FSharp.Charting.fsx"
open FSharp.Charting

/// Combine two paths together
let inline (@@) p1 p2 = System.IO.Path.Combine(p1, p2)
let exportFolder = __SOURCE_DIRECTORY__ @@ "..\\..\\docs\\files\\img\\perfcharts\\"
System.IO.Directory.CreateDirectory(exportFolder)

// simple plot function
let plot yaxis (metric : PerfResult -> float) (results : PerfResult list) =
    let values =
        results
        |> List.choose (fun r -> if r.HasFailed then None else Some (r.SessionId, metric r))
        |> List.sortBy snd

    let name = results |> List.tryPick (fun r -> Some r.TestId)
    let ch = Chart.Bar(values, ?Name = name, ?Title = name, YTitle = yaxis)
             |> Chart.With3D()
             |> Chart.WithXAxis(
                    Log=false,
                    MajorTickMark=ChartTypes.TickMark(Interval=1., LineWidth=2),
                    LabelStyle=ChartTypes.LabelStyle(Interval=1.))
    
    use f = ch.ShowChart()

    let nameFixedForExport = name.Value.ToString().Replace("[|\"","_").Replace("\"|]", "_") + ".png"
    let fileName = exportFolder @@ nameFixedForExport
    System.Console.WriteLine("saving {0}", fileName)
    ch.SaveChartAs (fileName, ChartTypes.ChartImageFormat.Png)
    
// read performance tests from 'Tests' module and run them
let allTestResults = comparers |> List.map (fun (tt, c) -> c.GetTestResults()) |> List.concat
// save the results as an XML file
TestSession.toFile (exportFolder @@ "perftests.xml") allTestResults
// graph them
allTestResults
|> TestSession.groupByTest
|> Map.iter (fun _ r -> plot "milliseconds" (fun t -> t.Elapsed.TotalMilliseconds) r)
