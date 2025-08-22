module Partas.GitNet.Markdown

open FSharp.Formatting.Markdown

module Implementation =
    type MarkdownBuilderBase<'T>() =
        member inline _.Zero(): unit = ()
        member inline _.Yield(_: unit): 'T list = []
        member inline _.Yield(value: 'T): 'T list = [ value ] 
        member inline _.Combine(_: unit, value: 'T) = [ value ]
        member inline _.Combine(value: 'T, _: unit) = [ value ]
        member inline _.Combine(left: 'T, right: 'T) = [ left;right ]
        member inline _.Combine(left: 'T list, right: 'T) =  left @ [ right ]
        member inline _.Combine(left: 'T, right: 'T list) = left :: right
        member inline _.Combine(left: 'T list, right: 'T list) = left @ right
        member inline _.Combine(left: 'T list, _: unit) = left
        member inline _.Combine(_: unit, right: 'T list) = right
        member inline _.Combine(y) = fun () -> y
        member inline _.Delay([<InlineIfLambda>] value) = value()
        member inline _.For(state: 'T list, [<InlineIfLambda>] value: unit -> 'T list) = value() |> List.append state
        member inline _.For(state: 'T list, [<InlineIfLambda>] value: 'T -> 'T list) =
            state |> List.collect value
    type MarkdownBuilderSpanStringBase<'T>(func) =
        inherit MarkdownBuilderBase<MarkdownSpan>()
        member inline _.Yield(value: string) = [ MarkdownSpan.Literal(value, None) ]
        member inline _.For(state: MarkdownSpans, [<InlineIfLambda>] value: string -> MarkdownSpans) =
            value >> (@) state
        [<CustomOperation "br">]
        member inline _.BreakOp(state: MarkdownSpans) =
            state @ [ MarkdownSpan.Literal("\n", None) ]
        member _.Run(state: MarkdownSpans): 'T = func state
    type MarkdownListBuilder() =
        inherit MarkdownBuilderBase<MarkdownParagraphs>()
        member inline _.Yield(value: string) =
            MarkdownParagraph.Paragraph([ MarkdownSpan.Literal(value, None) ], None)
            |> List.singleton
            |> List.singleton
        member inline _.Yield(value: MarkdownSpan) =
            MarkdownParagraph.Paragraph([ value ], None)
            |> List.singleton
            |> List.singleton
        member inline _.Yield(value: MarkdownSpans) =
            MarkdownParagraph.Paragraph(value, None)
            |> List.singleton
            |> List.singleton
        member inline _.Yield(value: MarkdownParagraph) =
            [ value ]
            |> List.singleton
    type TableRowBaseBuilder<'T>(func) =
        inherit MarkdownListBuilder()
        member _.Run(state: MarkdownParagraphs list): 'T = func state

open Implementation
module Markdown =
    type TableHeaders = TableHeaders of MarkdownParagraphs list
    type TableRow = TableRow of MarkdownParagraphs list
    type HeaderBuilder(?value: int) =
        inherit MarkdownBuilderSpanStringBase<MarkdownParagraph>
            (fun inp -> MarkdownParagraph.Heading(value |> Option.defaultValue 1, inp, None))
    type EmphasisBuilder() =
        inherit MarkdownBuilderSpanStringBase<MarkdownSpan>(fun inp -> MarkdownSpan.Emphasis(inp,None))
    type StrongBuilder() =
        inherit MarkdownBuilderSpanStringBase<MarkdownSpan>(fun inp -> MarkdownSpan.Strong(inp, None))
    type DirectLinkBuilder(link: string) =
        inherit MarkdownBuilderBase<MarkdownSpan>()
        let mutable title: string option = None
        member inline _.Yield(value: string) = [ MarkdownSpan.Literal(value, None) ]
        member inline _.For(state: MarkdownSpans, [<InlineIfLambda>] value: string -> MarkdownSpans) =
            value >> (@) state
        [<CustomOperation "title">]
        member _.TitleOp(state, value: string) =
            title <- Some value
            state
        member _.Run(state: MarkdownSpans) =
            MarkdownSpan.DirectLink(state,link,title,None)
    type DetailsBuilder() =
        inherit MarkdownBuilderBase<MarkdownParagraph>()
        member inline private _.MakeSummary(value: string) =
            $"<summary>{value}</summary>"
        member inline private _.MakeSummary(value: MarkdownParagraph) =
            [ MarkdownParagraph.InlineHtmlBlock("<summary>", None, None)
              value
              MarkdownParagraph.InlineHtmlBlock("</summary>", None, None) ]
        member inline private _.MakeSummary(value: MarkdownSpan) =
            MarkdownParagraph.Paragraph([
                MarkdownSpan.Literal("<summary>", None)
                value
                MarkdownSpan.Literal("</summary>", None)
            ], None) |> List.singleton
        member inline _.Yield(value: string) = [ MarkdownParagraph.Span(
                                                     [ MarkdownSpan.Literal(value, None) ], None) ]
        member inline _.Yield(value: MarkdownSpan) =
            [ MarkdownParagraph.Span([ value ], None) ]
        member inline _.Yield(value: MarkdownSpans) =
            [ MarkdownParagraph.Paragraph(value, None) ]
        member inline _.For(state: MarkdownParagraphs, [<InlineIfLambda>] value: string -> MarkdownParagraphs) =
            value >> (@) state
        [<CustomOperation "summary">]
        member inline this.SummaryOp(state: MarkdownParagraphs, value: string) =
            [ MarkdownParagraph.InlineHtmlBlock(this.MakeSummary value, None, None) ] |> (@) state
        [<CustomOperation "summary">]
        member inline this.SummaryOp(state: MarkdownParagraphs, value: MarkdownParagraph) =
            this.MakeSummary value |> (@) state
        [<CustomOperation "summary">]
        member inline this.SummaryOp(state: MarkdownParagraphs, value: MarkdownSpan) =
            this.MakeSummary value |> (@) state
        [<CustomOperation "br">]
        member inline this.BreakOp(state: MarkdownParagraphs) =
            state @ [ MarkdownParagraph.Paragraph([], None) ]
        member _.Run(state: MarkdownParagraphs) =
            [
                MarkdownParagraph.InlineHtmlBlock("<details>", None, None)
                yield! state
                MarkdownParagraph.InlineHtmlBlock("</details>", None, None)
            ]
    type ParagraphBuilder() =
        inherit MarkdownBuilderSpanStringBase<MarkdownParagraph>
            (fun inp -> MarkdownParagraph.Paragraph(inp, None))
    type SpanBuilder() =
        inherit MarkdownBuilderSpanStringBase<MarkdownParagraph>
            (fun inp -> MarkdownParagraph.Span(inp, None))
    type UnorderedListBuilder() =
        inherit MarkdownListBuilder()
        member _.Run(state: MarkdownParagraphs list) =
            MarkdownParagraph.ListBlock(MarkdownListKind.Unordered, state, None)
    type OrderedListBuilder() =
        inherit MarkdownListBuilder()
        member _.Run(state: MarkdownParagraphs list) =
            MarkdownParagraph.ListBlock(MarkdownListKind.Ordered, state, None)
    type BlockQuoteBuilder() =
        inherit MarkdownBuilderBase<MarkdownParagraph>()
        member inline _.Yield(value: string) =
            MarkdownParagraph.Paragraph([ MarkdownSpan.Literal(value, None) ], None)
            |> List.singleton
        member inline _.Yield(value: MarkdownSpan) =
            MarkdownParagraph.Paragraph([ value ], None)
            |> List.singleton
        member inline _.Yield(value: MarkdownSpans) =
            MarkdownParagraph.Paragraph(value, None)
            |> List.singleton
        member _.Run(state: MarkdownParagraphs) =
            MarkdownParagraph.QuotedBlock(state, None)
    type MarkdownBuilder() =
        inherit MarkdownListBuilder()
        member _.Run(state: MarkdownParagraphs list) =
            state
            |> List.collect id
    type IndirectLinkBuilder() =
        inherit MarkdownBuilderBase<MarkdownSpan>()
        let mutable original: string option = None
        let mutable key: string option = None
        member inline _.Yield(value: string) = [ MarkdownSpan.Literal(value, None) ]
        member inline _.For(state: MarkdownSpans, [<InlineIfLambda>] value: string -> MarkdownSpans) =
            value >> (@) state
        [<CustomOperation "br">]
        member inline _.BreakOp(state: MarkdownSpans) =
            state @ [ MarkdownSpan.Literal("\n", None) ]
        [<CustomOperation "original">]
        member _.OriginalOp(state: MarkdownSpans, value: string) =
            original <- Some value
            state
        [<CustomOperation "key">]
        member _.KeyOp(state: MarkdownSpans, value: string) =
            key <- Some value
            state
        member _.Run(state: MarkdownSpans) =
            MarkdownSpan.IndirectLink(state, original |> Option.defaultValue "", key |> Option.defaultValue "", None)
    type TableHeaderBuilder() =
        inherit TableRowBaseBuilder<TableHeaders>(TableHeaders)
    type TableRowBuilder() =
        inherit TableRowBaseBuilder<TableRow>(TableRow)
    type GenericRowBuilder() =
        inherit TableRowBaseBuilder<MarkdownTableRow>(id)
    type TableBuilder() =
        inherit MarkdownBuilderBase<unit>()
        let mutable headers: MarkdownTableRow option = None
        let mutable rows: MarkdownTableRow list = []
        let mutable alignments: MarkdownColumnAlignment list = []
        [<CustomOperation "alignments">]
        member _.AlignmentOp(_, value: MarkdownColumnAlignment list) = alignments <- value
        [<CustomOperation "headers">]
        member _.HeadersOp(_, value: MarkdownTableRow ) = headers <- Some value
        [<CustomOperation "row">]
        member _.RowOp(_, value: MarkdownTableRow ) = rows <- rows @ [ value ]
        member _.Zero() = ()
        member _.Yield(value: MarkdownColumnAlignment list) = alignments <- value
        member _.Yield(TableHeaders value) = headers <- Some value
        member _.Yield(TableRow value) = rows <- rows @ [ value ]
        member _.YieldFrom(value: TableRow list) =
            rows <-
                value |> List.map (function TableRow value -> value)
                |> (@) rows
        member inline _.Combine(_: unit, _: unit) = ()
        member _.Run _ =
            MarkdownParagraph.TableBlock(headers,alignments,rows,None)
    type table =
        static member row = TableRowBuilder()
        static member headers = TableHeaderBuilder()
type Markdown with
    static member h1 = Markdown.HeaderBuilder(1)
    static member h2 = Markdown.HeaderBuilder(2)
    static member h3 = Markdown.HeaderBuilder(3)
    static member h4 = Markdown.HeaderBuilder(4)
    static member h5 = Markdown.HeaderBuilder(5)
    static member h6 = Markdown.HeaderBuilder(6)
    static member h size = Markdown.HeaderBuilder(value = size)
    static member italics = Markdown.EmphasisBuilder()
    static member bold = Markdown.StrongBuilder()
    static member directLink link = Markdown.DirectLinkBuilder(link)
    static member anchorLink link = MarkdownSpan.AnchorLink(link, None)
    static member details = Markdown.DetailsBuilder()
    static member orderedList = Markdown.OrderedListBuilder()
    static member unorderedList = Markdown.UnorderedListBuilder()
    static member codeBlock language code = MarkdownParagraph.CodeBlock(code, None, Some "```", language, "", None)
    static member inlineCode code = MarkdownSpan.InlineCode(code, None)
    static member quote = Markdown.BlockQuoteBuilder()
    static member comment content = MarkdownSpan.Literal($"<!-- {content} -->", None)
    static member span = Markdown.SpanBuilder()
    static member para = Markdown.ParagraphBuilder()
    static member br = MarkdownSpan.HardLineBreak(None)
    static member document = Markdown.MarkdownBuilder()
    static member indirectLink = Markdown.IndirectLinkBuilder()
    static member ruler = MarkdownParagraph.HorizontalRule('-', None)
    static member literal content = MarkdownSpan.Literal(content, None)
    static member table = Markdown.TableBuilder()
    static member tableRow = Markdown.GenericRowBuilder()    
