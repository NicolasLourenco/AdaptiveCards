// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using AdaptiveExpressions;
using AdaptiveExpressions.Memory;
using AdaptiveExpressions.Properties;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AdaptiveCards.Templating
{
    /// <summary>
    /// an intance of this class is used in visiting a parse tree that's been generated by antlr4 parser
    /// </summary>
    public sealed class AdaptiveCardsTemplateVisitor : AdaptiveCardsTemplateParserBaseVisitor<AdaptiveCardsTemplateResult>
    {
        private Stack<DataContext> dataContext = new Stack<DataContext>();
        private readonly JToken root;
        private readonly Options options;
        private ArrayList templateVisitorWarnings;

        /// <summary>
        /// maintains data context
        /// </summary>
        private sealed class DataContext
        {
            public JToken token;
            public AdaptiveCardsTemplateSimpleObjectMemory AELMemory;
            public bool IsArrayType = false;

            public JToken RootDataContext;
            public const string rootKeyword = "$root";
            public const string dataKeyword = "$data";
            public const string indexKeyword = "$index";

            /// <summary>
            /// constructs a data context of which current data is jtoken
            /// </summary>
            /// <param name="jtoken">new data to kept as data context</param>
            /// <param name="rootDataContext">root data context</param>
            public DataContext(JToken jtoken, JToken rootDataContext)
            {
                Init(jtoken, rootDataContext);
            }

            /// <summary>
            /// overload contructor that takes <paramref name="text"/> which is <c>string</c>
            /// </summary>
            /// <exception cref="JsonException"><c>JToken.Parse(text)</c> can throw JsonException if <paramref name="text"/> is invalid json</exception>
            /// <param name="text">json in string</param>
            /// <param name="rootDataContext">a root data context</param>
            public DataContext(string text, JToken rootDataContext)
            {
                // disable date parsing handling
                var jsonReader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None };
                var jtoken = JToken.Load(jsonReader);
                Init(jtoken, rootDataContext);
            }

            /// <summary>
            /// Initializer method that takes jtoken and root data context to initialize a data context object
            /// </summary>
            /// <param name="jtoken">current data context</param>
            /// <param name="rootDataContext">root data context</param>
            private void Init(JToken jtoken, JToken rootDataContext)
            {
                AELMemory = (jtoken is JObject) ? new AdaptiveCardsTemplateSimpleObjectMemory(jtoken) : new AdaptiveCardsTemplateSimpleObjectMemory(new JObject());

                token = jtoken;
                RootDataContext = rootDataContext;

                if (jtoken is JArray)
                {
                    IsArrayType = true;
                }

                AELMemory.SetValue(dataKeyword, token);
                AELMemory.SetValue(rootKeyword, rootDataContext);
            }

            /// <summary>
            /// retrieve a <see cref="JObject"/> from this DataContext instance if <see cref="JToken"/> is a <see cref="JArray"/> at <paramref name="index"/>
            /// </summary>
            /// <param name="index"></param>
            /// <returns><see cref="JObject"/> at<paramref name="index"/> of a <see cref="JArray"/></returns>
            public DataContext GetDataContextAtIndex(int index)
            {
                var jarray = token as JArray;
                var jtokenAtIndex = jarray[index];
                var dataContext = new DataContext(jtokenAtIndex, RootDataContext);
                dataContext.AELMemory.SetValue(indexKeyword, index);
                return dataContext;
            }
        }

        /// <summary>
        /// a constructor for AdaptiveCardsTemplateVisitor
        /// </summary>
        /// <param name="nullSubstitutionOption">it will called upon when AEL finds no suitable functions registered in given AEL expression during evaluation the expression</param>
        /// <param name="data">json data in string which will be set as a root data context</param>
        public AdaptiveCardsTemplateVisitor(Func<string, object> nullSubstitutionOption, string data = null)
        {
            if (data?.Length != 0)
            {
                // set data as root data context
                try
                {
                    var jsonReader = new JsonTextReader(new StringReader(data)) { DateParseHandling = DateParseHandling.None };
                    root = JToken.Load(jsonReader);
                    PushDataContext(data, root);
                }
                catch (JsonException innerException)
                {
                    throw new AdaptiveTemplateException("Setting root data failed with given data context", innerException);
                }
                
            }

            // if null, set default option
            options = new Options
            {
                NullSubstitution = nullSubstitutionOption != null? nullSubstitutionOption : (path) => $"${{{path}}}"
            };

            templateVisitorWarnings = new ArrayList();
        }

        /// <summary>
        /// returns current data context
        /// </summary>
        /// <returns><see cref="DataContext"/></returns>
        private DataContext GetCurrentDataContext()
        {
            return dataContext.Count == 0 ? null : dataContext.Peek();
        }

        /// <summary>
        /// creates <see cref="JToken"/> object based on stringToParse, and pushes the object onto a stack
        /// </summary>
        /// <param name="stringToParse"></param>
        /// <param name="rootDataContext">current root data context</param>
        private void PushDataContext(string stringToParse, JToken rootDataContext)
        {
            dataContext.Push(new DataContext(stringToParse, rootDataContext));
        }

        /// <summary>
        /// push a <c>DataContext</c> onto a stack
        /// </summary>
        /// <param name="context"><c>context</c> to push</param>
        private void PushDataContext(DataContext context)
        {
            dataContext.Push(context);
        }

        /// <summary>
        /// Given a <paramref name="jpath"/>, create a new <see cref="DataContext"/> based on a current <see cref="DataContext"/>
        /// </summary>
        /// <param name="jpath">a json selection path</param>
        private void PushTemplatedDataContext(string jpath)
        {
            DataContext parentDataContext = GetCurrentDataContext();
            if (jpath == null || parentDataContext == null)
            {
                throw new ArgumentNullException("Parent data context or selection path is null");
            }

            var (value, error) = new ValueExpression("=" + jpath).TryGetValue(parentDataContext.AELMemory);
            if (error == null)
            {
                var serializedValue = JsonConvert.SerializeObject(value);
                dataContext.Push(new DataContext(serializedValue, parentDataContext.RootDataContext));
            }
            else
            {
                // if there was an error during parsing data, it's irrecoverable
                throw new Exception(error);
            }
        }

        /// <summary>
        /// Pops a data context
        /// </summary>
        private void PopDataContext()
        {
            dataContext.Pop();
        }

        /// <summary>
        /// Checks if there is a data context
        /// </summary>
        /// <returns></returns>
        private bool HasDataContext()
        {
            return dataContext.Count != 0;
        }

        /// <summary>
        /// Getter for templateVisitorWarnings
        /// </summary>
        /// <returns>ArrayList</returns>
        public ArrayList getTemplateVisitorWarnings()
        {
            return templateVisitorWarnings;
        }

        /// <summary>
        /// antlr runtime wil call this method when parse tree's context is <see cref="AdaptiveCardsTemplateParser.TemplateDataContext"/>
        /// <para>It is used in parsing a pair that has $data as key</para>
        /// <para>It creates new data context, and set it as current memory scope</para>
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override AdaptiveCardsTemplateResult VisitTemplateData([NotNull] AdaptiveCardsTemplateParser.TemplateDataContext context)
        {
            // checks if parsing failed, if failed, return failed segment as string unchanged
            if (!ValidateParserRuleContext(context))
            {
                return new AdaptiveCardsTemplateResult(context.GetText());
            }

            // get value node from pair node
            // i.e. $data : "value"
            IParseTree templateDataValueNode = context.value();
            // refer to label, valueTemplateStringWithRoot in AdaptiveCardsTemplateParser.g4 for the grammar this branch is checking
            if (templateDataValueNode is AdaptiveCardsTemplateParser.ValueTemplateStringWithRootContext)
            {
                // call a visit method for further processing
                Visit(templateDataValueNode);
            }
            // refer to label, valueTemplateString in AdaptiveCardsTemplateParser.g4 for the grammar this branch is checking
            else if (templateDataValueNode is AdaptiveCardsTemplateParser.ValueTemplateStringContext)
            {
                // tempalteString() can be zero or more due to user error
                var templateStrings = (templateDataValueNode as AdaptiveCardsTemplateParser.ValueTemplateStringContext).templateString();
                if (templateStrings?.Length == 1)
                {
                    // retrieve template literal and create a data context
                    var templateLiteral = (templateStrings[0] as AdaptiveCardsTemplateParser.TemplatedStringContext).TEMPLATELITERAL();
                    try
                    {
                        string templateLiteralExpression = templateLiteral.GetText(); 
                        PushTemplatedDataContext(templateLiteralExpression.Substring(2, templateLiteralExpression.Length - 3));
                    }
                    catch (ArgumentNullException)
                    {
                        throw new ArgumentNullException($"Check if parent data context is set, or please enter a non-null value for '{templateLiteral.Symbol.Text}' at line, '{templateLiteral.Symbol.Line}'");
                    }
                    catch (JsonException innerException)
                    {
                        throw new AdaptiveTemplateException($"'{templateLiteral.Symbol.Text}' at line, '{templateLiteral.Symbol.Line}' is malformed for '$data : ' pair", innerException);
                    }
                }
            }
            else
            // else clause handles all of the ordinary json values 
            {
                string childJson = templateDataValueNode.GetText();
                try
                {
                    PushDataContext(childJson, root);
                }
                catch (JsonException innerException)
                {
                    throw new AdaptiveTemplateException($"parsing data failed at line, '{context.Start.Line}', '{childJson}' was given", innerException);
                }
            }

            return new AdaptiveCardsTemplateResult();
        }

        /// <summary>
        /// Visitor method for <c>templateRoot</c> grammar in <c>AdaptiveCardsTemplateParser.g4</c>
        /// </summary>
        /// <param name="context"></param>
        /// <returns><see cref="AdaptiveCardsTemplateResult"/></returns>
        public override AdaptiveCardsTemplateResult VisitTemplateStringWithRoot([NotNull] AdaptiveCardsTemplateParser.TemplateStringWithRootContext context)
        {
            // checks if parsing failed, if failed, return failed segment as string unchanged
            if (!ValidateParserRuleContext(context))
            {
                return new AdaptiveCardsTemplateResult(context.GetText());
            }

            // retreives templateroot token from current context; please refers to templateRoot grammar in AdaptiveCardsTemplateParser.g4
            return Visit(context.TEMPLATEROOT());
        }

        /// <summary>
        /// Visitor method for <c>templateRootData</c> grammar rule in <c>AdaptiveCardsTemplateParser.g4</c>
        /// </summary>
        /// <param name="context"></param>
        /// <returns><see cref="AdaptiveCardsTemplateResult"/></returns>
        public override AdaptiveCardsTemplateResult VisitTemplateRootData([NotNull] AdaptiveCardsTemplateParser.TemplateRootDataContext context)
        {
            // checks if parsing failed, if failed, return failed segment as string unchanged
            if (!ValidateParserRuleContext(context))
            {
                return new AdaptiveCardsTemplateResult(context.GetText());
            }

            // retrieves templateRoot of the grammar as in this method's summary
            var child = context.templateRoot() as AdaptiveCardsTemplateParser.TemplateStringWithRootContext;
            try
            {
                PushTemplatedDataContext(child.GetText());
            }
            catch (ArgumentNullException)
            {
                throw new ArgumentException($"Check if parent data context is set, or please enter a non-null value for '{context.GetText()}' at line, '{context.Start.Line}'");
            }
            catch (JsonException innerException)
            {
                throw new AdaptiveTemplateException($"value of '$data : ', json pair, '{child.TEMPLATEROOT().Symbol.Text}' at line, '{child.TEMPLATEROOT().Symbol.Line}' is malformed", innerException);
            }

            return new AdaptiveCardsTemplateResult();
        }

        /// <summary>
        /// Visitor method for <c>valueTemplateExpresssion</c> grammar rule <c>AdaptiveCardsTemplateParser.g4</c>
        /// </summary>
        /// <remarks>parsed string has a form of "$when" : ${}</remarks>
        /// <param name="context"></param>
        /// <returns>AdaptiveCardsTemplateResult</returns>
        public override AdaptiveCardsTemplateResult VisitValueTemplateExpression([NotNull] AdaptiveCardsTemplateParser.ValueTemplateExpressionContext context)
        {
            // checks if parsing failed, if failed, return failed segment as string unchanged
            if (!ValidateParserRuleContext(context))
            {
                return new AdaptiveCardsTemplateResult(context.GetText());
            }

            // retreives TEMPLATELITERAL token and capture its content as AdaptiveCardsTemplateResult
            AdaptiveCardsTemplateResult result = new AdaptiveCardsTemplateResult(context.GetText(), context.TEMPLATELITERAL().GetText());

            DataContext dataContext = GetCurrentDataContext();

            // if current data context is array type, we can't evalute here, so we return the captured template expression
            if (dataContext == null || dataContext.IsArrayType)
            {
                return result;
            }

            bool isTrue = false;

            try
            {
                isTrue = IsTrue(result.Predicate, dataContext.token);
            }
            catch (System.FormatException)
            {
                templateVisitorWarnings.Add($"WARN: Could not evaluate {result.Predicate} because it could not be found in the provided data. " +
                                    "The condition has been set to false by default.");
            }

            // evaluate $when
            result.WhenEvaluationResult = isTrue ?
                AdaptiveCardsTemplateResult.EvaluationResult.EvaluatedToTrue :
                AdaptiveCardsTemplateResult.EvaluationResult.EvaluatedToFalse;

            return result;
        }

        /// <summary>
        /// Visitor method for <c>valueTemplateString</c> grammar rule <c>AdaptiveCardsTemplateParser.g4</c>
        /// </summary>
        /// <param name="context"></param>
        /// <returns><c>AdaptiveCardsTemplateResult</c></returns>
        public override AdaptiveCardsTemplateResult VisitValueTemplateString([NotNull] AdaptiveCardsTemplateParser.ValueTemplateStringContext context)
        {
            // checks if parsing failed, if failed, return failed segment as string unchanged
            if (!ValidateParserRuleContext(context))
            {
                return new AdaptiveCardsTemplateResult(context.GetText());
            }

            AdaptiveCardsTemplateResult result = new AdaptiveCardsTemplateResult();
            var templateStrings = context.templateString();
            if (templateStrings.Length == 1)
            {
                var templatedStringContext = templateStrings.GetValue(0) as AdaptiveCardsTemplateParser.TemplatedStringContext;
                // strictly, this check is not needed since the only children the context can have is this type
                if (templatedStringContext != null)
                {
                    ITerminalNode[] stringChildren = templatedStringContext.STRING();
                    // if ther are no string tokens, we do not quates
                    if (stringChildren.Length == 0 && HasDataContext())
                    {
                        result.Append(ExpandTemplatedString(templatedStringContext.TEMPLATELITERAL(), true));
                        return result;
                    }
                }
            }

            result.Append(context.StringDeclOpen().GetText());

            foreach (var templateString in templateStrings)
            {
                result.Append(Visit(templateString));
            }

            result.Append(context.CLOSE().GetText());

            return result;
        }

        /// <summary>
        /// Visitor method for <c>valueObject</c> grammar rule <c>AdaptiveCardsTemplateParser.g4</c>
        /// </summary>
        /// <param name="context"></param>
        /// <returns><c>AdaptiveCardsTemplateResult</c></returns>
        public override AdaptiveCardsTemplateResult VisitValueObject([NotNull] AdaptiveCardsTemplateParser.ValueObjectContext context)
        {
            // checks if parsing failed, if failed, return failed segment as string unchanged
            if (!ValidateParserRuleContext(context))
            {
                return new AdaptiveCardsTemplateResult(context.GetText());
            }

            return VisitObj(context.obj());
        }

        /// <summary>
        /// Visitor method for <c>obj</c> grammar rule <c>AdaptiveCardsTemplateParser.g4</c>
        /// </summary>
        /// <param name="context"></param>
        /// <returns><c>AdaptiveCardsTemplateResult</c></returns>
        public override AdaptiveCardsTemplateResult VisitObj([NotNull] AdaptiveCardsTemplateParser.ObjContext context)
        {
            // checks if parsing failed, if failed, return failed segment as string unchanged
            if (!ValidateParserRuleContext(context))
            {
                return new AdaptiveCardsTemplateResult(context.GetText());
            }

            var hasDataContext = false;
            var isArrayType = false;
            var pairs = context.pair();

            // pair that was used for data context
            AdaptiveCardsTemplateParser.PairContext dataPair = null;
            // find and set data context
            // visit the first data context available, the rest is ignored
            foreach (var pair in pairs)
            {
                if (pair is AdaptiveCardsTemplateParser.TemplateDataContext || pair is AdaptiveCardsTemplateParser.TemplateRootDataContext)
                {
                    if (pair.exception == null)
                    {
                        Visit(pair);
                        hasDataContext = true;
                        isArrayType = GetCurrentDataContext().IsArrayType;
                        dataPair = pair;
                        break;
                    }
                }
            }

            int repeatsCounts = 1;
            bool isObjAdded = false;
            var dataContext = GetCurrentDataContext();

            if (isArrayType && hasDataContext)
            {
                var jarray = dataContext.token as JArray;
                repeatsCounts = jarray.Count;
            }

            AdaptiveCardsTemplateResult combinedResult = new AdaptiveCardsTemplateResult();
            // indicates the number of removed json object(s)
            int removedCounts = 0;
            var comma = context.COMMA();
            string jsonPairDelimiter = (comma != null && comma.Length > 0) ? comma[0].GetText() : "";

            // loop for repeating obj parsed in the inner loop
            for (int iObj = 0; iObj < repeatsCounts; iObj++)
            {
                if (isArrayType)
                {
                    // set new data context
                    try
                    {
                        PushDataContext(dataContext.GetDataContextAtIndex(iObj));
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"setting data context failed with '{context.GetText()}' at line, '{context.Start.Line}'", e);
                    }
                }

                // parse obj
                AdaptiveCardsTemplateResult result = new AdaptiveCardsTemplateResult(context.LCB().GetText());
                var whenEvaluationResult = AdaptiveCardsTemplateResult.EvaluationResult.NotEvaluated;
                bool isPairAdded = false;

                for (int iPair = 0; iPair < pairs.Length; iPair++)
                {
                    var pair = pairs[iPair];
                    // if the pair refers to same pair that was used for data context, do not add its entry
                    if (pair != dataPair)
                    {
                        var returnedResult = Visit(pair);

                        // add a delimiter, e.g ',' before appending
                        // a pair after first pair is added
                        if (isPairAdded && !returnedResult.IsWhen)
                        {
                            result.Append(jsonPairDelimiter);
                        }

                        result.Append(returnedResult);

                        if (returnedResult.IsWhen)
                        {
                            if (returnedResult.WhenEvaluationResult == AdaptiveCardsTemplateResult.EvaluationResult.NotEvaluated)
                            {
                                // The when expression could not be evaluated, so we are defaulting the value to false
                                whenEvaluationResult = AdaptiveCardsTemplateResult.EvaluationResult.EvaluatedToFalse;

                                templateVisitorWarnings.Add($"WARN: Could not evaluate {returnedResult} because it is not an expression or the " +
                                    $"expression is invalid. The $when condition has been set to false by default.");
                                
                            }
                            else
                            {
                                whenEvaluationResult = returnedResult.WhenEvaluationResult;
                            }
                        }
                        else
                        {
                            isPairAdded = true;
                        }

                    }
                }

                result.Append(context.RCB().GetText());

                if (whenEvaluationResult != AdaptiveCardsTemplateResult.EvaluationResult.EvaluatedToFalse)
                {
                    if (isObjAdded)
                    {
                        // add a delimiter, e.g ',' before appending
                        // another object after first object is added
                        combinedResult.Append(jsonPairDelimiter);
                    }
                    combinedResult.Append(result);
                    isObjAdded = true;
                }
                else
                {
                    removedCounts++;
                }

                if (isArrayType)
                {
                    PopDataContext();
                }
            }

            if (hasDataContext)
            {
                PopDataContext();
            }

            // all existing json obj in input and repeated json obj if any have been removed 
            if (removedCounts == repeatsCounts)
            {
                combinedResult.HasItBeenDropped = true;
            }

            return combinedResult;
        }

        /// <summary>
        /// Visitor method for <c>ITernminalNode</c> 
        /// <para>collects token as string and expand template if needed</para>
        /// </summary>
        /// <param name="node"></param>
        /// <returns><c>AdaptiveCardsTemplateResult</c></returns>
        public override AdaptiveCardsTemplateResult VisitTerminal(ITerminalNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node.Symbol.Type == AdaptiveCardsTemplateLexer.TEMPLATELITERAL || node.Symbol.Type == AdaptiveCardsTemplateLexer.TEMPLATEROOT)
            {
                return ExpandTemplatedString(node);
            }

            return new AdaptiveCardsTemplateResult(node.GetText());
        }

        /// <summary>
        /// Visitor method for <c>templatdString</c> label in <c>AdaptiveCardsTemplateParser.g4</c>
        /// </summary>
        /// <param name="node"></param>
        /// <param name="isExpanded"></param>
        /// <returns><c>AdaptiveCardsTemplateResult</c></returns>
        public AdaptiveCardsTemplateResult ExpandTemplatedString(ITerminalNode node, bool isExpanded = false)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (HasDataContext())
            {
                DataContext currentDataContext = GetCurrentDataContext();
                return new AdaptiveCardsTemplateResult(Expand(Regex.Unescape(node.GetText()), currentDataContext.AELMemory, isExpanded, options));
            }

            return new AdaptiveCardsTemplateResult(node.GetText());
        }

        /// <summary>
        /// Expands template expression using Adaptive Expression Library (AEL)
        /// </summary>
        /// <param name="unboundString"></param>
        /// <param name="data"></param>
        /// <param name="isTemplatedString"></param>
        /// <param name="options"></param>
        /// <returns><c>string</c></returns>
        public static string Expand(string unboundString, IMemory data, bool isTemplatedString = false, Options options = null)
        {
            if (unboundString == null)
            {
                return "";
            }

            Expression exp;
            try
            {
                exp = Expression.Parse(unboundString.Substring(2, unboundString.Length - 3));
            }
            // AEL can throw any errors, for example, System.Data.Syntax error will be thrown from AEL's ANTLR 
            // when AEL encounters unknown functions.
            // We can't possibly know all errors and we simply want to leave the expression as it is when there are any exceptions
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return unboundString;
            }

            if (options == null)
            {
                options = new Options
                {
                    NullSubstitution = (path) => $"${{{path}}}"
                };
            }

            StringBuilder result = new StringBuilder();
            var (value, error) = exp.TryEvaluate(data, options);
            if (error == null)
            {
                // if isTemplatedString, it's a leaf node, and if it's string, the text should be wrapped with double quotes
                if (isTemplatedString && value is string) 
                {
                    result.Append('"');
                    result.Append(value);
                    result.Append('"');
                }
                else if (value is Boolean)
                {
                    result.Append(value.ToString().ToLower());
                }
                else
                {
                    result.Append(value);
                }
            }
            else
            {
                result.Append("${" + unboundString + "}");
            }

            return result.ToString();
        }

        /// <summary>
        /// return the parsed result of $when from pair context
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override AdaptiveCardsTemplateResult VisitTemplateWhen([NotNull] AdaptiveCardsTemplateParser.TemplateWhenContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            // when this node is visited, the children of this node is shown as below: 
            // this node is visited only when parsing was correctly done
            // [ '{', '$when', ':', ',', 'expression'] 
            var result = Visit(context.templateExpression());

            if (!result.IsWhen)
            {
                // We know that this result was supposed to be IsWhen since it is called from VisitTemplateWhen
                // We create a result with `IsWhen = false` if the expression is invalid
                // Result will now correctly follow the rest of the IsWhen logic
                result.IsWhen = true;
            }

            return result;
        }

        /// <summary>
        /// Visit method for <c>array</c> grammar in <c>AdaptiveCardsTemplateParser.g4</c>
        /// </summary>
        /// <param name="context"></param>
        /// <returns>AdaptiveCardsTemplateResult</returns>
        public override AdaptiveCardsTemplateResult VisitArray([NotNull] AdaptiveCardsTemplateParser.ArrayContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.exception != null)
            {
                return new AdaptiveCardsTemplateResult(context.GetText());
            }

            AdaptiveCardsTemplateResult result = new AdaptiveCardsTemplateResult(context.LSB().GetText());
            var values = context.value();
            var arrayDelimiters = context.COMMA();
            bool isValueAdded = false;

            // visit each json value in json array and integrate parsed result
            for (int i = 0; i < values.Length; i++)
            {
                var value = context.value(i);
                var parsedResult = Visit(value);

                // only add delimiter when parsedResult has not been dropped,
                // and a value has already been added to the array
                if (isValueAdded && !parsedResult.HasItBeenDropped && arrayDelimiters.Length > 0)
                {
                    result.Append(arrayDelimiters[0].GetText());
                }

                if (!parsedResult.HasItBeenDropped)
                {
                    result.Append(parsedResult);
                    isValueAdded = true;
                }
            }

            result.Append(context.RSB().GetText());

            return result;
        }

        /// <summary>
        /// Evaluates a predicate
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="data"></param>
        /// <returns><c>true</c> if predicate is evaluated to <c>true</c></returns>
        public static bool IsTrue(string predicate, JToken data)
        {
            var (value, error) = new ValueExpression(Regex.Unescape(predicate)).TryGetValue(data);
            if (error == null)
            {
                return bool.Parse(value as string);
            }
            return true;
        }

        /// <summary>
        /// Visits each children in IRuleNode
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public override AdaptiveCardsTemplateResult VisitChildren([NotNull] IRuleNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            AdaptiveCardsTemplateResult result = new AdaptiveCardsTemplateResult();

            for (int i = 0; i < node.ChildCount; i++)
            {
                result.Append(Visit(node.GetChild(i)));
            }

            return result;
        }

        private static bool ValidateParserRuleContext(Antlr4.Runtime.ParserRuleContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // checks if parsing failed, if failed, return failed segment as string unchanged
            if (context.exception != null)
            {
                return false;
            }

            return true;
        }
    }
}
