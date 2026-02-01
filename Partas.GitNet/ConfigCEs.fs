/// <summary>
/// Computation Expressions for configuring GitNet
/// </summary>
module Partas.GitNet.CE.Config

open System.Text.RegularExpressions
open System.ComponentModel
open Partas.GitNet

type CommitIgnoreBuilder() =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield (value: IgnoreCommit) = [ value ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.YieldFrom (values: IgnoreCommit list) = values
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield (_: unit): IgnoreCommit list = []
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Zero(): unit = ()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, value: IgnoreCommit) = [ value ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(value: IgnoreCommit, _: unit) = [ value ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: IgnoreCommit, r: IgnoreCommit) = [ l; r ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, value: IgnoreCommit list) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(value: IgnoreCommit list, _: unit) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: IgnoreCommit list, r: IgnoreCommit list) = l @ r
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: IgnoreCommit, r: IgnoreCommit list) = l :: r
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: IgnoreCommit list, r: IgnoreCommit) = r :: l
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(regex: Regex) = [ IgnoreCommit.SubjectRegex regex ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: IgnoreCommit list, [<InlineIfLambda>] value: unit -> IgnoreCommit list) = state @ value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: IgnoreCommit list, [<InlineIfLambda>] value: unit -> IgnoreCommit) = value() :: state
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> IgnoreCommit list) = value()
    [<CustomOperation("author")>]
    member inline _.AuthorOp(state: IgnoreCommit list, value: string) = IgnoreCommit.Author value :: state
    [<CustomOperation("email")>]
    member inline _.EmailOp(state: IgnoreCommit list, value: string) = IgnoreCommit.Email value :: state
    [<CustomOperation("type'")>]
    member inline _.TypeOp(state: IgnoreCommit list, value: string) = IgnoreCommit.Type value :: state
    [<CustomOperation("footerKey")>]
    member inline _.FooterKeyOp(state: IgnoreCommit list, value: string) = IgnoreCommit.FooterKey value :: state
    [<CustomOperation("footerKeyValue")>]
    member inline _.FooterKeyValueOp(state: IgnoreCommit list, key: string, value: string) = IgnoreCommit.FooterKeyValue(key, value) :: state
    [<CustomOperation("subjectRegex")>]
    member inline _.SubjectRegexOp(state: IgnoreCommit list, regex: Regex) = IgnoreCommit.SubjectRegex regex :: state
    [<CustomOperation("subjectRegex")>]
    member inline _.SubjectRegexOp(state: IgnoreCommit list, regex: string) = IgnoreCommit.SubjectRegex(Regex(regex)) :: state
    [<CustomOperation("sha")>]
    member inline _.ShaOp(state: IgnoreCommit list, value: string) = IgnoreCommit.Sha value :: state
    [<CustomOperation("sha")>]
    member inline _.ShaOp(state: IgnoreCommit list, value: int) = IgnoreCommit.Sha (value.ToString()) :: state
    [<CustomOperation("scope")>]
    member inline _.ScopeOp(state: IgnoreCommit list, value: string) = IgnoreCommit.CommitScope value :: state
    [<CustomOperation("skipci")>]
    member inline _.SkipCIOp(state: IgnoreCommit list) = IgnoreCommit.SkipCi :: state
    [<CustomOperation("timeRange")>]
    member inline _.TimeRangeOp(state: IgnoreCommit list, from: System.DateTimeOffset, timeSpan: System.TimeSpan) =
        IgnoreCommit.TimeRange(from, timeSpan) :: state
    [<CustomOperation("timeRange")>]
    member inline _.TimeRangeOp(state: IgnoreCommit list, from: System.DateTime, timeSpan: System.TimeSpan) =
        IgnoreCommit.TimeRange(System.DateTimeOffset from, timeSpan) :: state
    [<CustomOperation("timeRange")>]
    member inline _.TimeRangeOp(state: IgnoreCommit list, from: string, timeSpan: string) =
        IgnoreCommit.TimeRange(
            System.DateTimeOffset.Parse(from),
            System.TimeSpan.Parse(timeSpan)
        ) :: state
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(state: IgnoreCommit list) = state

/// <summary>
/// Internal CE use only
/// </summary>
[<EditorBrowsable(EditorBrowsableState.Never)>]
module CommitIgnoreFooterParts =
    [<Struct>]
    type CommitIgnoreFooterKey = CommitIgnoreFooterKey of string with
        member inline this.Value = let (CommitIgnoreFooterKey value) = this in value
    
    [<Struct>]
    type CommitIgnoreFooterValue = CommitIgnoreFooterValue of string with
        member inline this.Value = let (CommitIgnoreFooterValue value) = this in value
/// <summary>
/// Internal CE use only
/// </summary>
[<EditorBrowsable(EditorBrowsableState.Never)>]
type CommitIgnoreFooterParts = {
    Key: string 
    Value: string 
}
type CommitIgnoreFooterBuilder() =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield (value: CommitIgnoreFooterParts.CommitIgnoreFooterKey) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield (value: CommitIgnoreFooterParts.CommitIgnoreFooterValue) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield (value: CommitIgnoreFooterParts) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(_: unit) = ()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Zero() = ()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: unit, r: CommitIgnoreFooterParts.CommitIgnoreFooterKey) = r
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: unit, r: CommitIgnoreFooterParts.CommitIgnoreFooterValue) = r
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitIgnoreFooterParts.CommitIgnoreFooterKey, r: unit) = l
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitIgnoreFooterParts.CommitIgnoreFooterValue, r: unit) = l
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitIgnoreFooterParts, r: unit) = l
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: unit, r: CommitIgnoreFooterParts) = r
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitIgnoreFooterParts.CommitIgnoreFooterKey, r: CommitIgnoreFooterParts.CommitIgnoreFooterValue) =
        {
            Key = l.Value
            Value = r.Value
        }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitIgnoreFooterParts.CommitIgnoreFooterValue, r: CommitIgnoreFooterParts.CommitIgnoreFooterKey) =
        {
            Key = r.Value
            Value = l.Value
        }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitIgnoreFooterParts.CommitIgnoreFooterKey, [<InlineIfLambda>] value: unit -> CommitIgnoreFooterParts.CommitIgnoreFooterValue) =
        {
            Key = state.Value
            Value = value().Value
        }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitIgnoreFooterParts.CommitIgnoreFooterValue, [<InlineIfLambda>] value: unit -> CommitIgnoreFooterParts.CommitIgnoreFooterKey) =
        {
            Key = value().Value
            Value = state.Value
        }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(state: CommitIgnoreFooterParts) =
        IgnoreCommit.FooterKeyValue(state.Key, state.Value)
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(state: CommitIgnoreFooterParts.CommitIgnoreFooterKey) =
        IgnoreCommit.FooterKey state.Value
    /// <summary>
    /// Ignore commits with the footer key equal to the value
    /// </summary>
    [<CustomOperation "key">]
    member inline _.KeyOp(state: CommitIgnoreFooterParts.CommitIgnoreFooterValue, value: string) =
        {
            Key = value
            Value = state.Value
        }
    /// <summary>
    /// Ignore commits with the footer key equal to the value
    /// </summary>
    [<CustomOperation "key">]
    member inline _.KeyOp(_: unit, value: string) = CommitIgnoreFooterParts.CommitIgnoreFooterKey value
    /// <summary>
    /// Ignore commits with the footer value equal to the input
    /// </summary>
    [<CustomOperation "value">]
    member inline _.ValueOp(_: unit, value: string) = CommitIgnoreFooterParts.CommitIgnoreFooterValue value
    /// <summary>
    /// Ignore commits with the footer value equal to the input
    /// </summary>
    [<CustomOperation "value">]
    member inline _.ValueOp(state: CommitIgnoreFooterParts.CommitIgnoreFooterKey, value: string) =
        {
            Key = state.Value
            Value = value
        }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(value: string) = CommitIgnoreFooterParts.CommitIgnoreFooterKey value


[<EditorBrowsable(EditorBrowsableState.Never)>]
module CommitBumpTypeMappingBuilder =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    [<Struct>]
    type MajorBump = MajorBump of BumpMatcher with
        member inline this.Value = let (MajorBump value) = this in value
        static member inline Create value = MajorBump value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    [<Struct>]
    type MinorBump = MinorBump of BumpMatcher with
        member inline this.Value = let (MinorBump value) = this in value
        static member inline Create value = MinorBump value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    [<Struct>]
    type PatchBump = PatchBump of BumpMatcher with
        member inline this.Value = let (PatchBump value) = this in value
        static member inline Create value = PatchBump value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    [<Struct>]
    type EpochBump = EpochBump of EpochMatcher with
        member inline this.Value = let (EpochBump value) = this in value
        static member inline Create value = EpochBump value
type CommitBumpTypeMappingBuilder() =
    let empty = { Epoch = []; Major = []; Minor = []; Patch = [] }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Zero() = empty
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield (_: unit) = {
        Epoch = []
        Major = []
        Minor = []
        Patch = []
    }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(_: unit, value: CommitBumpTypeMapping) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(value: CommitBumpTypeMapping, _: unit) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine (l: CommitBumpTypeMapping, r: CommitBumpTypeMapping) = {
        Epoch = l.Epoch @ r.Epoch
        Major = l.Major @ r.Major
        Minor = l.Minor @ r.Minor
        Patch = l.Patch @ r.Patch
    }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay ([<InlineIfLambda>] value: unit -> CommitBumpTypeMapping) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay ([<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.EpochBump) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay ([<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.MajorBump) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay ([<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.PatchBump) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay ([<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.MinorBump) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(value: CommitBumpTypeMappingBuilder.EpochBump) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(value: CommitBumpTypeMappingBuilder.PatchBump) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(value: CommitBumpTypeMappingBuilder.MinorBump) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(value: CommitBumpTypeMappingBuilder.MajorBump) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: CommitBumpTypeMappingBuilder.EpochBump list) = { empty with Epoch = value |> List.map _.Value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: CommitBumpTypeMappingBuilder.PatchBump list) = { empty with Patch = value |> List.map _.Value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: CommitBumpTypeMappingBuilder.MinorBump list) = { empty with Minor = value |> List.map _.Value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: CommitBumpTypeMappingBuilder.MajorBump list) = { empty with Major = value |> List.map _.Value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, value: CommitBumpTypeMappingBuilder.EpochBump) =
        { empty with Epoch = [ value.Value ] }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, value: CommitBumpTypeMappingBuilder.PatchBump) =
        { empty with Patch = [ value.Value ] }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, value: CommitBumpTypeMappingBuilder.MinorBump) =
        { empty with Minor = [ value.Value ] }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, value: CommitBumpTypeMappingBuilder.MajorBump) =
        { empty with Major = [ value.Value ] }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(value: CommitBumpTypeMappingBuilder.EpochBump, _: unit) =
        { empty with Epoch = [ value.Value ] }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(value: CommitBumpTypeMappingBuilder.PatchBump, _: unit) =
        { empty with Patch = [ value.Value ] }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(value: CommitBumpTypeMappingBuilder.MinorBump, _: unit) =
        { empty with Minor = [ value.Value ] }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(value: CommitBumpTypeMappingBuilder.MajorBump, _: unit) =
        { empty with Major = [ value.Value ] }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(l: CommitBumpTypeMapping, r: CommitBumpTypeMappingBuilder.EpochBump) = { l with Epoch = r.Value :: l.Epoch }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(l: CommitBumpTypeMapping, r: CommitBumpTypeMappingBuilder.PatchBump) = { l with Patch = r.Value :: l.Patch }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(l: CommitBumpTypeMapping, r: CommitBumpTypeMappingBuilder.MinorBump) = { l with Minor = r.Value :: l.Minor }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(l: CommitBumpTypeMapping, r: CommitBumpTypeMappingBuilder.MajorBump) = { l with Major = r.Value :: l.Major }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(r: CommitBumpTypeMappingBuilder.EpochBump, l: CommitBumpTypeMapping) = { l with Epoch = r.Value :: l.Epoch }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(r: CommitBumpTypeMappingBuilder.PatchBump, l: CommitBumpTypeMapping) = { l with Patch = r.Value :: l.Patch }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(r: CommitBumpTypeMappingBuilder.MinorBump, l: CommitBumpTypeMapping) = { l with Minor = r.Value :: l.Minor }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(r: CommitBumpTypeMappingBuilder.MajorBump, l: CommitBumpTypeMapping) = { l with Major = r.Value :: l.Major }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitBumpTypeMapping, [<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.EpochBump) = { state with Epoch = value().Value :: state.Epoch }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitBumpTypeMapping, [<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.PatchBump) = { state with Patch = value().Value :: state.Patch }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitBumpTypeMapping, [<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.MinorBump) = { state with Minor = value().Value :: state.Minor }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitBumpTypeMapping, [<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.MajorBump) = { state with Major = value().Value :: state.Major }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitBumpTypeMapping, [<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.EpochBump list) = { state with Epoch = (value() |> List.map _.Value) @ state.Epoch }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitBumpTypeMapping, [<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.PatchBump list) = { state with Patch = (value() |> List.map _.Value) @ state.Patch }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitBumpTypeMapping, [<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.MinorBump list) = { state with Minor = (value() |> List.map _.Value) @ state.Minor }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitBumpTypeMapping, [<InlineIfLambda>] value: unit -> CommitBumpTypeMappingBuilder.MajorBump list) = { state with Major = (value() |> List.map _.Value) @ state.Major }
    [<CustomOperation "defaults">]
    member inline _.AddDefaultsOp(state: CommitBumpTypeMapping) =
        { state with
              Epoch = state.Epoch @ CommitBumpTypeMapping.init.Epoch
              Patch = state.Patch @ CommitBumpTypeMapping.init.Patch
              Minor = state.Minor @ CommitBumpTypeMapping.init.Minor
              Major = state.Major @ CommitBumpTypeMapping.init.Major }
    [<CustomOperation "epoch">]
    member inline _.EpochOp(state: CommitBumpTypeMapping, value: string) = { state with Epoch = EpochMatcher value :: state.Epoch }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(state: CommitBumpTypeMapping) = state

type CommitBumpMatcherBuilderBase<^T when ^T:(static member Create: BumpMatcher -> ^T)>() =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(_: unit): ^T list = []
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(value: BumpMatcher) = [ 'T.Create value ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(value: CommitIgnoreFooterParts) =
        [
            BumpMatcher.FooterKeyValue(
                value.Key,
                value.Value
            )
            |> 'T.Create
        ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(value: CommitIgnoreFooterParts.CommitIgnoreFooterKey) = 'T.Create (BumpMatcher.FooterKey value.Value)
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(_: unit, value: BumpMatcher) = [ 'T.Create value ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(value: BumpMatcher, _: unit) = [ 'T.Create value ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(l: ^T list, r: BumpMatcher) = 'T.Create r :: l
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(l: BumpMatcher, r: ^T list) = 'T.Create l :: r
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(l: ^T list, r: ^T list) = l @ r
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Zero(): ^T list = []
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay ([<InlineIfLambda>] value: unit -> ^T list) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: ^T list, [<InlineIfLambda>] value: unit -> BumpMatcher) = 'T.Create (value()) :: state
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: ^T list, [<InlineIfLambda>] value: unit -> ^T list) = value() @ state
    [<CustomOperation "type'">]
    member inline _.TypeOp(state: ^T list, value: string) = 'T.Create (BumpMatcher.Type value) :: state
    [<CustomOperation "descriptionRegex">]
    member inline _.DescriptionRegexOp(state: ^T list, value: string) = 'T.Create (BumpMatcher.DescriptionRegex value) :: state
    [<CustomOperation "typeAndDescriptionRegex">]
    member inline _.TypeAndDescriptionRegexOp(state: ^T list, typ: string, regex: string) = 'T.Create (BumpMatcher.TypeDescription(typ, regex)) :: state
    [<CustomOperation "typeAndFooterKey">]
    member inline _.TypeAndFooterKeyOp(state: ^T list, typ: string, key: string) = 'T.Create (BumpMatcher.TypeFooterKey(typ, key)) :: state
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(state: ^T list) = state
type MajorCommitBumpMatcherBuilder() = inherit CommitBumpMatcherBuilderBase<CommitBumpTypeMappingBuilder.MajorBump>()
type MinorCommitBumpMatcherBuilder() = inherit CommitBumpMatcherBuilderBase<CommitBumpTypeMappingBuilder.MinorBump>()
type PatchCommitBumpMatcherBuilder() = inherit CommitBumpMatcherBuilderBase<CommitBumpTypeMappingBuilder.PatchBump>()

[<EditorBrowsable(EditorBrowsableState.Never)>]
module CommitGroupBuilder =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    [<Struct>]
    type Title = Title of string with
        member inline this.Value = let (Title value) = this in value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    [<Struct>]
    type Position = Position of int with
        member inline this.Value = let (Position value) = this in value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    [<Struct>]
    type HeadingLevel = HeadingLevel of int with
        member inline this.Value = let (HeadingLevel value) = this in value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    [<Struct>]
    type Prelude = Prelude of string with
        member inline this.Value = let (Prelude value) = this in value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    [<Struct>]
    type Postfix = Postfix of string with
        member inline this.Value = let (Postfix value) = this in value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    [<Struct>]
    type CountOnly = CountOnly 
type CommitGroupBuilder(title: string) =
    let init = CommitGroup.init title
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Zero() = init
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(_: unit) = ()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: CommitGroup) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value: unit -> CommitGroup) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitGroup, CommitGroupBuilder.Title r) = { l with Title = r }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitGroup, CommitGroupBuilder.Position r) = { l with Position = Some r }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitGroup, CommitGroupBuilder.HeadingLevel r) = { l with HeadingLevel = r }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitGroup, CommitGroupBuilder.Prelude r) = { l with Prelude = Some r }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitGroup, CommitGroupBuilder.Postfix r) = { l with Postfix = Some r }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitGroup, CommitGroupBuilder.CountOnly) = { l with CountOnly = true }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitGroup, [<InlineIfLambda>] value: unit -> CommitGroupBuilder.Title) = { state with Title = value().Value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitGroup, [<InlineIfLambda>] value: unit -> CommitGroupBuilder.Position) = { state with Position = Some (value().Value) }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitGroup, [<InlineIfLambda>] value: unit -> CommitGroupBuilder.HeadingLevel) = { state with HeadingLevel = value().Value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitGroup, [<InlineIfLambda>] value: unit -> CommitGroupBuilder.Prelude) = { state with Prelude = Some (value().Value) }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitGroup, [<InlineIfLambda>] value: unit -> CommitGroupBuilder.Postfix) = { state with Postfix = Some (value().Value) }
    [<CustomOperation "position">]
    member inline _.PositionOp(state: CommitGroup, value: int) = { state with Position = Some value }
    [<CustomOperation "position">]
    member _.PositionOp(_: unit, value: int) = { init with Position = Some value }
    [<CustomOperation "title">]
    member inline _.TitleOp(state: CommitGroup, value: string) = { state with Title = value }
    [<CustomOperation "title">]
    member _.TitleOp(_: unit, value: string) = { init with Title = value }
    [<CustomOperation "headingLevel">]
    member inline _.HeadingLevelOp(state: CommitGroup, value: int) = { state with HeadingLevel = value }
    [<CustomOperation "headingLevel">]
    member _.HeadingLevelOp(_: unit, value: int) = { init with HeadingLevel = value }
    [<CustomOperation "prelude">]
    member inline _.PreludeOp(state: CommitGroup, value: string) = { state with Prelude = Some value }
    [<CustomOperation "prelude">]
    member _.PreludeOp(_: unit, value: string) = { init with Prelude = Some value }
    [<CustomOperation "postfix">]
    member inline _.PostfixOp(state: CommitGroup, value: string) = { state with Postfix = Some value }
    [<CustomOperation "postfix">]
    member _.PostfixOp(_: unit, value: string) = { init with Postfix = Some value }
    [<CustomOperation "countOnly">]
    member inline _.CountOnlyOp(state: CommitGroup) = { state with CountOnly = true }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(state: CommitGroup) = state
type GroupMatcherBuilder(group: CommitGroup) =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(_: unit): BumpMatcher list = []
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(value: BumpMatcher) = [ value ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(value: CommitIgnoreFooterParts) =
        [ BumpMatcher.FooterKeyValue(value.Key, value.Value) ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(value: CommitIgnoreFooterParts.CommitIgnoreFooterKey) = [ BumpMatcher.FooterKey value.Value ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(_: unit, value: BumpMatcher) = [ value ]
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(l: BumpMatcher list, r: BumpMatcher) = r :: l
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Zero(): BumpMatcher list = []
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay ([<InlineIfLambda>] value: unit -> BumpMatcher list) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(l: BumpMatcher, r: BumpMatcher list) = l :: r
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: BumpMatcher list, [<InlineIfLambda>] value: unit -> BumpMatcher) = value() :: state
    [<CustomOperation "type'">]
    member inline _.TypeOp(state: BumpMatcher list, value: string) = BumpMatcher.Type value :: state
    [<CustomOperation "descriptionRegex">]
    member inline _.DescriptionRegexOp(state: BumpMatcher list, value: string) = BumpMatcher.DescriptionRegex value :: state
    [<CustomOperation "typeAndDescriptionRegex">]
    member inline _.TypeAndDescriptionRegexOp(state: BumpMatcher list, typ: string, regex: string) = BumpMatcher.TypeDescription(typ, regex) :: state
    [<CustomOperation "typeAndFooterKey">]
    member inline _.TypeAndFooterKeyOp(state: BumpMatcher list, typ: string, key: string) = BumpMatcher.TypeFooterKey(typ, key) :: state
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Run(state: BumpMatcher list) = GroupMatcher(group, state)
    

type OutputConfigBuilder() =
    let init = OutputConfig.init
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> OutputConfig) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> IgnoreCommit list) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> GroupMatcher) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> CommitGroup) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: OutputConfig, [<InlineIfLambda>] value: unit -> IgnoreCommit list) =
        { state with Ignore = value() @ state.Ignore }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: OutputConfig, [<InlineIfLambda>] value: unit -> GroupMatcher) =
        { state with GroupMatcher = value() :: state.GroupMatcher }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: OutputConfig, [<InlineIfLambda>] value: unit -> CommitGroup) =
        { state with DefaultUnmatchedGroup = value() }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value: unit -> OutputConfig) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value: unit -> IgnoreCommit list) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value: unit -> GroupMatcher) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value: unit -> CommitGroup) = value()
    [<CustomOperation "path">]
    member inline _.PathOp(state: OutputConfig, value: string) = { state with Path = value }
    [<CustomOperation "path">]
    member _.PathOp(_: unit, value: string) = { init with Path = value }
    [<CustomOperation "path">]
    member _.PathOp(state: IgnoreCommit list, value: string) = { init with Path = value; Ignore = state @ init.Ignore }
    [<CustomOperation "path">]
    member _.PathOp(state: GroupMatcher, value: string) = { init with Path = value; GroupMatcher = state :: init.GroupMatcher }
    [<CustomOperation "path">]
    member _.PathOp(state: CommitGroup, value: string) = { init with Path = value; DefaultUnmatchedGroup = state }
    [<CustomOperation "filterUnconventionalCommits">]
    member inline _.FilterUnconventionalCommitsOp(state: OutputConfig) = { state with AllowUnconventional = false }
    [<CustomOperation "filterUnconventionalCommits">]
    member _.FilterUnconventionalCommitsOp(_: unit) = { init with AllowUnconventional = false }
    [<CustomOperation "filterUnconventionalCommits">]
    member _.FilterUnconventionalCommitsOp(state: IgnoreCommit list) = { init with AllowUnconventional = false; Ignore = state @ init.Ignore }
    [<CustomOperation "filterUnconventionalCommits">]
    member _.FilterUnconventionalCommitsOp(state: GroupMatcher) = { init with AllowUnconventional = false; GroupMatcher = state :: init.GroupMatcher }
    [<CustomOperation "filterUnconventionalCommits">]
    member _.FilterUnconventionalCommitsOp(state: CommitGroup) = { init with AllowUnconventional = false; DefaultUnmatchedGroup = state }
    [<CustomOperation "allowUnconventionalCommits">]
    member _.AllowUnconventionalCommitsOp(state: OutputConfig, ?value: bool) = { state with AllowUnconventional = defaultArg value true }
    [<CustomOperation "allowUnconventionalCommits">]
    member _.AllowUnconventionalCommitsOp(_: unit, ?value: bool) = { init with AllowUnconventional = defaultArg value true }
    [<CustomOperation "allowUnconventionalCommits">]
    member _.AllowUnconventionalCommitsOp(state: IgnoreCommit list, ?value: bool) = { init with AllowUnconventional = defaultArg value true; Ignore = state @ init.Ignore }
    [<CustomOperation "allowUnconventionalCommits">]
    member _.AllowUnconventionalCommitsOp(state: GroupMatcher, ?value: bool) = { init with AllowUnconventional = defaultArg value true; GroupMatcher = state :: init.GroupMatcher }
    [<CustomOperation "allowUnconventionalCommits">]
    member _.AllowUnconventionalCommitsOp(state: CommitGroup, ?value: bool) = { init with AllowUnconventional = defaultArg value true; DefaultUnmatchedGroup = state }
    [<CustomOperation "allowUngroupedCommits">]
    member _.AllowUngroupedCommitsOp(state: OutputConfig, ?value: bool) = { state with AllowUnmatched = defaultArg value true }
    [<CustomOperation "allowUngroupedCommits">]
    member _.AllowUngroupedCommitsOp(_: unit, ?value: bool) = { init with AllowUnmatched = defaultArg value true }
    [<CustomOperation "allowUngroupedCommits">]
    member _.AllowUngroupedCommitsOp(state: IgnoreCommit list, ?value: bool) = { init with AllowUnmatched = defaultArg value true; Ignore = state @ init.Ignore }
    [<CustomOperation "allowUngroupedCommits">]
    member _.AllowUngroupedCommitsOp(state: GroupMatcher, ?value: bool) = { init with AllowUnmatched = defaultArg value true; GroupMatcher = state :: init.GroupMatcher }
    [<CustomOperation "allowUngroupedCommits">]
    member _.AllowUngroupedCommitsOp(state: CommitGroup, ?value: bool) = { init with AllowUnmatched = defaultArg value true; DefaultUnmatchedGroup = state }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(_: unit) = init
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(groupMatcher: GroupMatcher) = groupMatcher
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(ignoreCommit: IgnoreCommit list) = ignoreCommit
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(commitGroup: CommitGroup) = commitGroup
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(outputConfig: OutputConfig) = outputConfig
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, groupMatcher: GroupMatcher) = { init with GroupMatcher = groupMatcher :: init.GroupMatcher }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, ignoreCommit: IgnoreCommit list) = { init with Ignore = ignoreCommit @ init.Ignore |> List.distinct }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, commitGroup: CommitGroup) = { init with DefaultUnmatchedGroup = commitGroup }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(state: OutputConfig, groupMatcher: GroupMatcher) = { state with GroupMatcher = groupMatcher :: state.GroupMatcher }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(state: OutputConfig, ignoreCommit: IgnoreCommit list) = { state with Ignore = ignoreCommit @ state.Ignore |> List.distinct }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(state: OutputConfig, commitGroup: CommitGroup) = { state with DefaultUnmatchedGroup = commitGroup }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(groupMatcher: GroupMatcher, state: OutputConfig) = { state with GroupMatcher = groupMatcher :: state.GroupMatcher }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(ignoreCommit: IgnoreCommit list, state: OutputConfig) = { state with Ignore = ignoreCommit @ state.Ignore |> List.distinct }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(commitGroup: CommitGroup, state: OutputConfig) = { state with DefaultUnmatchedGroup = commitGroup }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Run(state: OutputConfig) = state
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Run(ignoreCommits: IgnoreCommit list) = { init with Ignore = ignoreCommits }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Run(groupMatcher: GroupMatcher) = { init with GroupMatcher = groupMatcher :: init.GroupMatcher }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Run(commitGroup: CommitGroup) = { init with DefaultUnmatchedGroup = commitGroup }

type BumpConfigBuilder() =
    let init = {
        Mapping = {
            Epoch = []
            Major = []
            Minor = []
            Patch = []
        }
        DefaultBumpStrategy = ForceBumpStrategy.Auto
    }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(_: unit) = ()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: CommitBumpTypeMapping) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value: unit -> CommitBumpTypeMapping) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: ForceBumpStrategy) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value: unit -> ForceBumpStrategy) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value: unit -> unit) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value: unit -> BumpConfig) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitBumpTypeMapping, r: ForceBumpStrategy) = { Mapping = l; DefaultBumpStrategy = r }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(r: ForceBumpStrategy, l: CommitBumpTypeMapping) = { Mapping = l; DefaultBumpStrategy = r }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: CommitBumpTypeMapping, _: unit) = l
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: ForceBumpStrategy, _: unit) = l
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, r: ForceBumpStrategy) = r
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, l: CommitBumpTypeMapping) = l
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Zero() = init
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Run(state: CommitBumpTypeMapping) = { init with Mapping = state }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Run(state: ForceBumpStrategy) = { init with DefaultBumpStrategy = state }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Run(state: BumpConfig) = state
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> ForceBumpStrategy) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: CommitBumpTypeMapping, [<InlineIfLambda>] value: unit -> ForceBumpStrategy) = { Mapping = state; DefaultBumpStrategy = value() }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: BumpConfig, [<InlineIfLambda>] value: unit -> ForceBumpStrategy) = { state with DefaultBumpStrategy = value() }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> CommitBumpTypeMapping) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: ForceBumpStrategy, [<InlineIfLambda>] value: unit -> CommitBumpTypeMapping) = { Mapping = value(); DefaultBumpStrategy = state }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: BumpConfig, [<InlineIfLambda>] value: unit -> CommitBumpTypeMapping) = { state with Mapping = value() }
    [<CustomOperation "defaults">]
    member _.AddDefaultsOp(_: unit) = BumpConfig.init
    [<CustomOperation "addDefaults">]
    member _.AddDefaultsOp(state: CommitBumpTypeMapping) =
        { BumpConfig.init with
              Mapping = {
                state with
                    Epoch = state.Epoch @ BumpConfig.init.Mapping.Epoch
                    Major = state.Major @ BumpConfig.init.Mapping.Major
                    Minor = state.Minor @ BumpConfig.init.Mapping.Minor
                    Patch = state.Patch @ BumpConfig.init.Mapping.Patch
            } }
    [<CustomOperation "addDefaults">]
    member _.AddDefaultsOp(state: BumpConfig) =
        { state with
              Mapping = {
                state.Mapping with
                    Epoch = state.Mapping.Epoch @ BumpConfig.init.Mapping.Epoch
                    Major = state.Mapping.Major @ BumpConfig.init.Mapping.Major
                    Minor = state.Mapping.Minor @ BumpConfig.init.Mapping.Minor
                    Patch = state.Mapping.Patch @ BumpConfig.init.Mapping.Patch
            } }
    [<CustomOperation "addDefaults">]
    member _.AddDefaultsOp(state: ForceBumpStrategy) = { BumpConfig.init with DefaultBumpStrategy = state }
    [<CustomOperation "defaultBumpStrategy">]
    member _.DefaultBumpStrategyOp(_: unit, ?value: ForceBumpStrategy) = Option.defaultValue BumpConfig.init.DefaultBumpStrategy value
    [<CustomOperation "defaultBumpStrategy">]
    member _.DefaultBumpStrategyOp(state: CommitBumpTypeMapping, ?value: ForceBumpStrategy) = { Mapping = state; DefaultBumpStrategy = Option.defaultValue BumpConfig.init.DefaultBumpStrategy value }
    [<CustomOperation "defaultBumpStrategy">]
    member _.DefaultBumpStrategyOp(state: BumpConfig, value: ForceBumpStrategy) = { state with DefaultBumpStrategy = value }

type ProjectConfigBuilder() =
    let init = ProjectConfig.init
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: FSharpNameResolution) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, value: FSharpNameResolution) = { init with NameResolution = value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(value: FSharpNameResolution, _: unit) = { init with NameResolution = value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(value: FSharpNameResolution, config: ProjectConfig) = { config with NameResolution = value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(config: ProjectConfig, value: FSharpNameResolution) = { config with NameResolution = value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Zero() = init
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(_: unit) = init
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> FSharpNameResolution) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(state: ProjectConfig, [<InlineIfLambda>] value: unit -> FSharpNameResolution) = { state with NameResolution = value() }
    [<CustomOperation "autoScopeFn">]
    member inline _.AutoScopeOp(state: ProjectConfig, [<InlineIfLambda>] fn: string -> string option) = { state with AutoScoping = fn }
    [<CustomOperation "autoScopeFn">]
    member _.AutoScopeOp(_: unit, fn: string -> string option) = { init with AutoScoping = fn }
    [<CustomOperation "ignoreProject">]
    member inline _.IgnoreProjectOp(state: ProjectConfig, value: string) = { state with IgnoredProjects = value :: state.IgnoredProjects }
    [<CustomOperation "ignoreProject">]
    member _.IgnoreProjectOp(_: unit, value: string) = { init with IgnoredProjects = value :: init.IgnoredProjects }
    [<CustomOperation "ignoreProjects">]
    member _.IgnoreProjectsOp(state: ProjectConfig, projects: string list) = { state with IgnoredProjects = projects }
    [<CustomOperation "ignoreProjects">]
    member _.IgnoreProjectsOp(_: unit, projects: string list) = { init with IgnoredProjects = projects }
    [<CustomOperation "overrideExplicitScopes">]
    member _.OverrideExplicitScopesOp(state: ProjectConfig) = { state with OverrideExplicitScopes = true }
    [<CustomOperation "overrideExplicitScopes">]
    member _.OverrideExplicitScopesOp(_: unit) = { init with OverrideExplicitScopes = true }
    [<CustomOperation "respectExplicitScopes">]
    member _.RespectExplicitScopesOp(state: ProjectConfig) = { state with OverrideExplicitScopes = false }
    [<CustomOperation "respectExplicitScopes">]
    member _.RespectExplicitScopesOp(_: unit) = { init with OverrideExplicitScopes = false }
    [<CustomOperation "nameResolution">]
    member _.NameResolutionOp(state: ProjectConfig, value: FSharpNameResolution) = { state with NameResolution = value }
    [<CustomOperation "nameResolution">]
    member _.NameResolutionOp(_: unit, value: FSharpNameResolution) = { init with NameResolution = value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Run(state: ProjectConfig) = state

[<EditorBrowsable(EditorBrowsableState.Never)>]
module GitNetConfigBuilder =
   type GitNetConfigConstructor = {
       RepositoryPath: string voption
       Scope: ScopeStrategy voption
       InitialVersionStrategy: ProjectInitialVersionStrategy voption
       Output: OutputConfig voption
       Projects: ProjectConfig voption
       Bump: BumpConfig voption
       AssemblyFiles: AssemblyFileManagement voption
       Network: NetworkConfig voption
       WriteVersionToProjects: bool voption
   } with
       static member Empty = {
           RepositoryPath = ValueNone
           Scope = ValueNone
           InitialVersionStrategy = ValueNone
           Output = ValueNone
           Projects = ValueNone
           Bump = ValueNone
           AssemblyFiles = ValueNone
           Network = ValueNone
           WriteVersionToProjects = ValueNone
       }
open GitNetConfigBuilder
type GitNetConfigBuilder() =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(_: unit) = GitNetConfigConstructor.Empty
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(scope: ScopeStrategy) = { GitNetConfigConstructor.Empty with Scope = ValueSome scope }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: ProjectInitialVersionStrategy) = { GitNetConfigConstructor.Empty with InitialVersionStrategy = ValueSome value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: OutputConfig) = { GitNetConfigConstructor.Empty with Output = ValueSome value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: ProjectConfig) = { GitNetConfigConstructor.Empty with Projects = ValueSome value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: BumpConfig) = { GitNetConfigConstructor.Empty with Bump = ValueSome value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: AssemblyFileManagement) = { GitNetConfigConstructor.Empty with AssemblyFiles = ValueSome value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Yield(value: NetworkConfig) = { GitNetConfigConstructor.Empty with Network = ValueSome value }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] value) = value()
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(_: unit, value: GitNetConfigConstructor) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(value: GitNetConfigConstructor, _: unit) = value
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Combine(l: GitNetConfigConstructor, r: GitNetConfigConstructor) = {
            RepositoryPath = r.RepositoryPath |> ValueOption.orElse l.RepositoryPath
            Scope = r.Scope |> ValueOption.orElse l.Scope
            InitialVersionStrategy = r.InitialVersionStrategy |> ValueOption.orElse l.InitialVersionStrategy
            Output = r.Output |> ValueOption.orElse l.Output
            Projects = r.Projects |> ValueOption.orElse l.Projects
            Bump = r.Bump |> ValueOption.orElse l.Bump
            AssemblyFiles = r.AssemblyFiles |> ValueOption.orElse l.AssemblyFiles
            Network = r.Network |> ValueOption.orElse l.Network
            WriteVersionToProjects = r.WriteVersionToProjects |> ValueOption.orElse l.WriteVersionToProjects
    }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> ScopeStrategy) = { GitNetConfigConstructor.Empty with Scope = ValueSome <| value() }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> ProjectInitialVersionStrategy) = { GitNetConfigConstructor.Empty with InitialVersionStrategy = ValueSome <| value() }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> OutputConfig) = { GitNetConfigConstructor.Empty with Output = ValueSome <| value() }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> ProjectConfig) = { GitNetConfigConstructor.Empty with Projects = ValueSome <| value() }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> BumpConfig) = { GitNetConfigConstructor.Empty with Bump = ValueSome <| value() }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> AssemblyFileManagement) = { GitNetConfigConstructor.Empty with AssemblyFiles = ValueSome <| value() }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(_: unit, [<InlineIfLambda>] value: unit -> NetworkConfig) = { GitNetConfigConstructor.Empty with Network = ValueSome <| value() }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(l: GitNetConfigConstructor, [<InlineIfLambda>] value: unit -> GitNetConfigConstructor) =
        let r = value()
        {
            RepositoryPath = r.RepositoryPath |> ValueOption.orElse l.RepositoryPath
            Scope = r.Scope |> ValueOption.orElse l.Scope
            InitialVersionStrategy = r.InitialVersionStrategy |> ValueOption.orElse l.InitialVersionStrategy
            Output = r.Output |> ValueOption.orElse l.Output
            Projects = r.Projects |> ValueOption.orElse l.Projects
            Bump = r.Bump |> ValueOption.orElse l.Bump
            AssemblyFiles = r.AssemblyFiles |> ValueOption.orElse l.AssemblyFiles
            Network = r.Network |> ValueOption.orElse l.Network
            WriteVersionToProjects = r.WriteVersionToProjects |> ValueOption.orElse l.WriteVersionToProjects
        }
    [<CustomOperation "repositoryPath">]
    member _.RepositoryPathOp(state: GitNetConfigConstructor, value: string) = { state with RepositoryPath = ValueSome value }
    [<CustomOperation "repositoryPath">]
    member _.RepositoryPathOp(_: unit, value: string) = { GitNetConfigConstructor.Empty with RepositoryPath = ValueSome value }
    [<CustomOperation "initialVersionStrategy">]
    member _.InitialVersionStrategyOp(state: GitNetConfigConstructor, value: ProjectInitialVersionStrategy) = { state with InitialVersionStrategy = ValueSome value }
    [<CustomOperation "initialVersionStrategy">]
    member _.InitialVersionStrategyOp(_: unit, value: ProjectInitialVersionStrategy) = { GitNetConfigConstructor.Empty with InitialVersionStrategy = ValueSome value }
    [<CustomOperation "assemblyFileStrategy">]
    member _.AssemblyFilesOp(state: GitNetConfigConstructor, value: AssemblyFileManagement) = { state with AssemblyFiles = ValueSome value }
    [<CustomOperation "assemblyFileStrategy">]
    member _.AssemblyFilesOp(_: unit, value: AssemblyFileManagement) = { GitNetConfigConstructor.Empty with AssemblyFiles = ValueSome value }
    [<CustomOperation "writeVersionToProjects">]
    member _.WriteVersionToProjectsOp(state: GitNetConfigConstructor, ?value: bool) =
        let value = defaultArg value true
        { state with WriteVersionToProjects = ValueSome value }
    [<CustomOperation "writeVersionToProjects">]
    member _.WriteVersionToProjectsOp(_: unit, ?value: bool) =
        let value = defaultArg value true
        { GitNetConfigConstructor.Empty with WriteVersionToProjects = ValueSome value }
    [<CustomOperation "githubUrl">]
    member _.GithubUrlOp(state: GitNetConfigConstructor, value: string) = { state with Network = ValueSome <| { Github = { Url = GithubUrlStrategy.Manual value }  } }
    [<CustomOperation "githubUrl">]
    member _.GithubUrlOp(_: unit, value: string) = { GitNetConfigConstructor.Empty with Network = ValueSome <| { Github = { Url = GithubUrlStrategy.Manual value }  } }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member _.Run(state: GitNetConfigConstructor): GitNetConfig =
        let init: GitNetConfig = GitNetConfig.init true
        {
            RepositoryPath = (state.RepositoryPath |> ValueOption.defaultValue init.RepositoryPath)
            Scope = (state.Scope |> ValueOption.defaultValue init.Scope)
            InitialVersionStrategy = (state.InitialVersionStrategy |> ValueOption.defaultValue init.InitialVersionStrategy)
            Output = (state.Output |> ValueOption.defaultValue init.Output)
            Projects = (state.Projects |> ValueOption.defaultValue init.Projects)
            Bump = (state.Bump |> ValueOption.defaultValue init.Bump)
            AssemblyFiles = (state.AssemblyFiles |> ValueOption.defaultValue init.AssemblyFiles)
            Network = (state.Network |> ValueOption.defaultValue init.Network)
            WriteVersionToProjects = (state.WriteVersionToProjects |> ValueOption.defaultValue init.WriteVersionToProjects)
        }
/// <summary>
/// Computation Expression to build a list of IgnoreCommit settings.
/// </summary>
/// <example>
/// <code lang="fsharp">
/// {
///     CommitConfig.Ignore =
///         ignoreCommits {
///             skipci
///             tag "v1.0.0"
///             footer {
///                 key "BREAKING CHANGE"
///             }
///         }
///         // The CE produces a list of IgnoreCommit DUs
/// }
/// </code>
/// 1. Skips commits with the [skipci] header.<br/>
/// 2. Skips commits for the tag v1.0.0.<br/>
/// 3. Skips commits with a footer key of "BREAKING CHANGE".
/// </example>
let ignoreCommits = CommitIgnoreBuilder()
/// <summary>
/// Computation Expression to build a GroupMatcher, which is used to determine which commits belong to which group.
/// </summary>
let groupMatcher = GroupMatcherBuilder
/// <summary>
/// Computation Expression to build a footer key or key value pair.
/// Used with <c>ignoreCommits</c>, <c>groupMatcher</c> and others.
/// </summary>
let footer = CommitIgnoreFooterBuilder()
/// <summary>
/// Computation Expression to build the <c>OutputConfig</c> record.
/// </summary>
let outputConfig = OutputConfigBuilder()
/// <summary>
/// Computation Expression to build bump mappings for minor bumps
/// </summary>
let minors = MinorCommitBumpMatcherBuilder()
/// <summary>
/// Computation Expression to build bump mappings for patch bumps
/// </summary>
let patches = PatchCommitBumpMatcherBuilder()
/// <summary>
/// Computation Expression to build bump mappings for major bumps
/// </summary>
let majors = MajorCommitBumpMatcherBuilder()
/// <summary>
/// Computation Expression to build bump mappings for commit messages.
/// Use the <c>epoch</c> custom operation in cooperation with the <c>minors</c>, <c>patches</c> and <c>majors</c> CE's.
/// </summary>
let commitBumpMatcher = CommitBumpTypeMappingBuilder()
/// <summary>
/// Computation Expression to build a <c>CommitGroup</c> record.
/// </summary>
let commitGroup = CommitGroupBuilder
/// <summary>
/// Computation Expression to build a <c>BumpConfig</c> record.
/// </summary>
let bumpConfig = BumpConfigBuilder()
/// <summary>
/// Computation Expression to build a <c>ProjectConfig</c> record.
/// </summary>
let projectConfig = ProjectConfigBuilder()
/// <summary>
/// Computation Expression to build a <c>GitNetConfig</c> record.
/// </summary>
let gitnetConfig = GitNetConfigBuilder()