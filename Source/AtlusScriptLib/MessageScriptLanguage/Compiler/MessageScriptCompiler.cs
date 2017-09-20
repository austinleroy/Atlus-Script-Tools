﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using AtlusScriptLib.Common.Logging;
using AtlusScriptLib.MessageScriptLanguage.BinaryModel;
using AtlusScriptLib.MessageScriptLanguage.Parser;

namespace AtlusScriptLib.MessageScriptLanguage.Compiler
{
    public class MessageScriptCompiler
    {
        private Logger mLogger;
        private MessageScriptBinaryFormatVersion mVersion;

        public MessageScriptCompiler( MessageScriptBinaryFormatVersion version )
        {
            mVersion = version;
            mLogger = new Logger( nameof( MessageScriptCompiler ) );
            LoggerManager.RegisterLogger( mLogger );
        }

        public void AddListener( LogListener listener )
        {
            listener.Subscribe( mLogger );
        }

        // Todo: improve error logging in general
        // Todo: add exception settings?
        public MessageScript Compile( string input )
        {
            if ( !TryCompile(input, out var script))
                throw new MessageScriptCompilationFailureException();

            return script;
        }

        public MessageScript Compile( TextReader input )
        {
            if ( !TryCompile( input, out var script ) )
                throw new MessageScriptCompilationFailureException();

            return script;
        }

        public MessageScript Compile( Stream input )
        {
            if ( !TryCompile( input, out var script ) )
                throw new MessageScriptCompilationFailureException();

            return script;
        }

        public bool TryCompile( string input, out MessageScript script )
        {
            var cst = MessageScriptParserHelper.ParseCompilationUnit( input );
            return TryCompile( cst, out script );
        }

        public bool TryCompile( TextReader input, out MessageScript script )
        {
            var cst = MessageScriptParserHelper.ParseCompilationUnit( input );
            return TryCompile( cst, out script );
        }

        public bool TryCompile( Stream input, out MessageScript script )
        {
            var cst = MessageScriptParserHelper.ParseCompilationUnit( input );
            return TryCompile( cst, out script );
        }

        private bool TryCompile( MessageScriptParser.CompilationUnitContext context, out MessageScript script )
        {
            if ( !TryCompileImpl( context, out script ) )
            {
                LogError( context, "Failed to compile message script" );
                return false;
            }

            return true;
        }

        private bool TryCompileImpl( MessageScriptParser.CompilationUnitContext context, out MessageScript script )
        {
            LogContextInfo( context );

            script = null;

            if ( !TryGetFatal( context, () => context.messageWindow(), "Expected message dialog window", out var messageWindowContexts))
            {
                return false;
            }

            script = new MessageScript();
            script.FormatVersion = mVersion;

            foreach ( var messageWindowContext in messageWindowContexts )
            {
                IMessageScriptWindow messageWindow;

                if ( TryGet( messageWindowContext, () => messageWindowContext.dialogWindow(), out var dialogWindowContext))
                {
                    if ( !TryCompile( dialogWindowContext, out var dialogWindow ) )
                    {
                        LogError( dialogWindowContext, "Failed to compile dialog window" );
                        return false;
                    }

                    messageWindow = dialogWindow;
                }
                else if ( TryGet( messageWindowContext, () => messageWindowContext.selectionWindow(), out var selectionWindowContext ) )
                {
                    if ( !TryCompile( selectionWindowContext, out var selectionWindow ) )
                    {
                        LogError( selectionWindowContext, "Failed to compile selection window" );
                        return false;
                    }

                    messageWindow = selectionWindow;
                }
                else
                {
                    LogError( messageWindowContext, "Expected dialog or selection window" );
                    return false;
                }

                script.Windows.Add( messageWindow );
            }

            return true;
        }

        private bool TryCompile( MessageScriptParser.DialogWindowContext context, out MessageScriptDialogWindow dialogWindow )
        {
            LogContextInfo( context );

            dialogWindow = null;

            //
            // Parse identifier
            //
            string identifier;
            {
                if ( !TryGetFatal( context, () => context.Identifier(), "Expected dialog window name", out var identifierNode ) )
                    return false;

                identifier = identifierNode.Symbol.Text;
            }

            //
            // Parse speaker name
            //
            IMessageScriptSpeaker speaker = null;
            if ( TryGet( context, () => context.dialogWindowSpeakerName(), out var speakerNameContentContext ) )
            {
                if ( !TryGetFatal( speakerNameContentContext, () => speakerNameContentContext.tagText(), "Expected dialog window speaker name text", out var speakerNameTagTextContext ) )
                    return false;

                if ( !TryCompileLines( speakerNameTagTextContext, out var speakerNameLines ) )
                {
                    LogError( speakerNameContentContext, "Failed to parse dialog window speaker name" );
                    return false;
                }

                if ( speakerNameLines.Count != 0 )
                {
                    if ( speakerNameLines.Count > 1 )
                        LogWarning( speakerNameTagTextContext, "More than 1 line for dialog window speaker name. Only the 1st line will be used" );

                    speaker = new MessageScriptNamedSpeaker( speakerNameLines[0] );
                }
            }

            // 
            // Parse text content
            //
            List<MessageScriptLine> lines;
            {
                if ( !TryGetFatal( context, () => context.tagText(), "Expected dialog window text", out var tagTextContext ) )
                    return false;

                if ( !TryCompileLines( tagTextContext, out lines ) )
                {
                    LogError( tagTextContext, "Failed to parse dialog window text" );
                    return false;
                }
            }

            //
            // Create dialog window
            //
            dialogWindow = new MessageScriptDialogWindow( identifier, speaker, lines );

            return true;
        }

        private bool TryCompile( MessageScriptParser.SelectionWindowContext context, out MessageScriptSelectionWindow selectionWindow )
        {          
            LogContextInfo( context );

            selectionWindow = null;

            //
            // Parse identifier
            //
            string identifier;
            {
                if ( !TryGetFatal( context, () => context.Identifier(), "Expected selection window name", out var identifierNode ) )
                    return false;

                identifier = identifierNode.Symbol.Text;
            }

            // 
            // Parse text content
            //
            List<MessageScriptLine> lines;
            {
                if ( !TryGetFatal( context, () => context.tagText(), "Expected selection window text", out var tagTextContext ) )
                    return false;

                if ( !TryCompileLines( tagTextContext, out lines ) )
                {
                    LogError( tagTextContext, "Failed to parse selection window text" );
                    return false;
                }
            }

            //
            // Create Selection window
            //
            selectionWindow = new MessageScriptSelectionWindow( identifier, lines );

            return true;
        }

        private bool TryCompileLines( MessageScriptParser.TagTextContext context, out List<MessageScriptLine> lines )
        {
            LogContextInfo( context );

            lines = new List<MessageScriptLine>();
            MessageScriptLineBuilder lineBuilder = null;

            foreach ( var node in context.children )
            {
                IMessageScriptLineToken lineToken = null;

                if ( TryCast<MessageScriptParser.TagContext>( node, out var tagContext ) )
                {
                    if ( !TryGetFatal( context, () => tagContext.Identifier(), "Expected tag id", out var tagIdNode ) )
                        return false;

                    var tagId = tagIdNode.Symbol.Text;

                    switch ( tagId.ToLowerInvariant() )
                    {
                        case "f":
                            TryCompile( tagContext, out var functionToken );
                            lineToken = functionToken;
                            break;

                        case "n":
                            lineToken = new MessageScriptNewLineToken();
                            break;

                        case "e":
                            {
                                if ( lineBuilder == null )
                                {
                                    LogWarning( context, "Empty line" );
                                    lines.Add( new MessageScriptLine() );
                                }
                                else
                                {
                                    lines.Add( lineBuilder.Build() );
                                    lineBuilder = null;
                                }

                                continue;
                            }

                        default:
                            {
                                LogError( tagContext, $"Unknown tag with id {tagId}" );
                                return false;
                            }
                    }
                }
                else if ( TryCast<ITerminalNode>( node, out var textNode ) )
                {
                    var text = textNode.Symbol.Text;

                    var textWithoutNewlines = Regex.Replace( text, @"\r?\n", "" );
                    if ( textWithoutNewlines.Length != 0 ) // filter out standalone newlines
                        lineToken = new MessageScriptTextToken( textWithoutNewlines );
                    else
                        continue;
                }
                else
                {
                    if ( node is ParserRuleContext )
                    {
                        LogError( node as ParserRuleContext, "Expected a tag or text, but got neither." );
                    }
                    else
                    {
                        LogError( context, "Expected a tag or text, but got neither." );
                    }

                    return false;
                }

                if ( lineBuilder == null )
                    lineBuilder = new MessageScriptLineBuilder();

                if ( lineToken != null )
                {
                    lineBuilder.AddToken( lineToken );
                }
                else
                {
#if DEBUG
                    Debugger.Break();
#endif
                }
            }

            if ( lineBuilder != null )
            {
                lines.Add( lineBuilder.Build() );
            }

            return true;
        }

        private bool TryCompile( MessageScriptParser.TagContext context,  out MessageScriptFunctionToken functionToken )
        {
            LogContextInfo( context );

            functionToken = new MessageScriptFunctionToken();

            if ( !TryGetFatal( context, () => context.IntLiteral(), "Expected arguments", out var argumentNodes ) )
                return false;

            if ( !TryParseIntLiteral( context, "Expected function table index", () => argumentNodes[0], out var functionTableIndex ) )
                return false;

            if ( !TryParseIntLiteral( context, "Expected function index", () => argumentNodes[1], out var functionIndex ) )
                return false;

            if ( argumentNodes.Length > 2 )
            {
                List<short> arguments = new List<short>( argumentNodes.Length - 2 );
                for ( int i = 2; i < argumentNodes.Length; i++ )
                {
                    if ( !TryParseIntLiteral( context, "Expected function argument", () => argumentNodes[i], out var argument ) )
                        return false;

                    arguments.Add( argument );
                }

                functionToken = new MessageScriptFunctionToken( functionTableIndex, functionIndex, arguments );
            }
            else
            {
                functionToken = new MessageScriptFunctionToken( functionTableIndex, functionIndex );
            }

            return true;
        }

        private bool TryGetFatal<T>( ParserRuleContext context, Func<T> getFunc, string failureText, out T value )
        {
            bool success = TryGet( context, getFunc, out value );

            if ( !success )
                LogError( context, failureText );

            return success;
        }

        private bool TryGet<T>( ParserRuleContext context, Func<T> getFunc, out T value )
        {
            try
            {
                value = getFunc();
            }
            catch ( Exception e )
            {
                value = default( T );
                return false;
            }

            if ( value == null )
                return false;

            return true;
        }

        private bool TryCast<T>( object obj, out T value ) where T : class
        {
            value = obj as T;
            return value != null;
        }

        private bool TryParseIntLiteral( ParserRuleContext context, string failureText, Func<ITerminalNode> getFunc, out short value )
        {
            value = -1;

            if ( !TryGetFatal( context, getFunc, failureText, out var node ) )
                return false;

            if ( !TryParseIntLiteral( node, out value ) )
                return false;

            return true;
        }

        private bool TryParseIntLiteral( ITerminalNode node, out short value )
        {
            bool succeeded;
            int intValue;

            if ( node.Symbol.Text.StartsWith( "0x" ) )
            {
                succeeded = int.TryParse( node.Symbol.Text.Substring( 2 ), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out intValue );
            }
            else
            {
                succeeded = int.TryParse( node.Symbol.Text, out intValue );
            }

            if ( !succeeded )
            {
                LogError( node.Symbol, "Invalid integer format" );
            }

            /*
            if ( intValue < ushort.MinValue || intValue > ushort.MaxValue )
            {
                LogError( node.Symbol, "Integer outside of signed short range" );
                succeeded = false;
            }
            */

            value = ( short )intValue;
            return succeeded;
        }

        private void LogContextInfo( ParserRuleContext context )
        {
            mLogger.Info( $"Compiling {MessageScriptParser.ruleNames[context.RuleIndex]} ({context.Start.Line}:{context.Start.Column})" );
        }

        private void LogError( ParserRuleContext context, string str )
        {
            mLogger.Error( $"{str} ({context.Start.Line}:{context.Start.Column})" );
        }

        private void LogError( IToken token, string str )
        {
            mLogger.Error( $"{str} ({token.Line}:{token.Column})" );
        }

        private void LogWarning( ParserRuleContext context, string str )
        {
            mLogger.Warning( $"{str} ({context.Start.Line}:{context.Start.Column})" );
        }
    }

    public class MessageScriptCompilationFailureException : Exception
    {
        public MessageScriptCompilationFailureException()
            : base("Failed to compile message script")
        {
        }
    }
}
