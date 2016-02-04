[<AutoOpen>]
module Helpers

open Fixie
open Ploeh.AutoFixture
open Ploeh.AutoFixture.Kernel
open Moq
open Ploeh.AutoFixture.AutoMoq
open Swensen.Unquote
open System
open System.Reflection
open FlexSearch.Api.Model
open FlexSearch.Core
open FlexSearch.CsvConnector
open System.Collections.Generic

[<AutoOpen>]
module Funcs =
    let isSuccessResponse (r : ResponseContext<'T>) =
        match r with
        | SuccessResponse(_) -> true
        | SomeResponse(rb,sc,fc) -> rb |> succeeded
        | _ -> false

/// Autofixture customizations
let fixtureCustomization() = 
    let fixture = (new Fixture()).Customize(new AutoConfiguredMoqCustomization())

    fixture.Inject<LuceneAnalyzer>(new FlexLucene.Analysis.Standard.StandardAnalyzer())
    fixture.Inject<IReadOnlyDictionary<string, Predicate * SearchQuery>>(new Dictionary<string, Predicate * SearchQuery>())

    // TODO solve all the dependencies that FlexSearch Core needs

    fixture.Freeze<Mock<IIndexService>>()
           .Setup(fun s -> s.IndexOnline(It.IsAny<string>()))
           .Returns(okUnit)
    |> ignore
    
    fixture.Freeze<Mock<IJobService>>()
           .Setup(fun s -> s.UpdateJob(It.IsAny<Job>()))
           .Returns(okUnit)
    |> ignore

    fixture.Freeze<Mock<IQueueService>>()
           .Setup(fun s -> s.AddDocumentQueue(It.IsAny<Document>()))
    |> ignore                          

    // We override Auto fixture's string generation mechanism to return this string which will be
    // used as index name
    fixture.Register<String>(fun _ -> Guid.NewGuid().ToString("N"))
    fixture

// ----------------------------------------------------------------------------
// Convention Section for Fixie
// ----------------------------------------------------------------------------
/// Custom attribute to create parameterised test
[<AttributeUsage(AttributeTargets.Method, AllowMultiple = true)>]
type InlineDataAttribute([<System.ParamArrayAttribute>] parameters : obj []) = 
    inherit Attribute()
    member val Parameters = parameters

type InputParameterSource() = 
    interface ParameterSource with
        member __.GetParameters(methodInfo : MethodInfo) = 
            // Check if the method contains inline data attribute. If not then use AutoFixture
            // to generate input value
            let customAttribute = methodInfo.GetCustomAttributes<InlineDataAttribute>(true)
            if customAttribute |> Seq.isEmpty 
            then 
                let fixture = fixtureCustomization()
                let create (builder : ISpecimenBuilder, typ : Type) = (new SpecimenContext(builder)).Resolve(typ)
                let parameterTypes = methodInfo.GetParameters() |> Array.map (fun x -> x.ParameterType)
                let parameterValues = parameterTypes |> Array.map (fun x -> create (fixture, x))
                seq { yield parameterValues }
            else customAttribute |> Seq.map (fun input -> input.Parameters)

type SingleInstancePerClassConvention() as self = 
    inherit Convention()
    
    let fixtureFactory (typ : Type) = 
        let fixture = fixtureCustomization()
        (new SpecimenContext(fixture)).Resolve(typ)
    
    do
        self.Classes.NameEndsWith([| "Tests"; "Test"; "test"; "tests" |]) |> ignore
        self.ClassExecution.CreateInstancePerClass().UsingFactory(fun typ -> fixtureFactory (typ)) |> ignore
        self.Parameters.Add<InputParameterSource>() |> ignore