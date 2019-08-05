﻿using System;
using System.ComponentModel.Design;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using TestGenerator.Generation;
using TestGenerator.Parsing;
using Task = System.Threading.Tasks.Task;

namespace TestGenerator.Commands
{
    /// <summary>
    /// Command handler
    /// </summary>
    public sealed class UnitTestGenerationCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("4b7d1ede-9a1d-4fc1-84df-9e91107236be");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage _package;

        /// <summary>
        /// Loads the syntax tree for the given document
        /// </summary>
        private readonly ISyntaxTreeFactory _syntaxTreeFactory;

        /// <summary>
        /// Loads specific data from the syntax tree
        /// </summary>
        private readonly IClassParser _classParser;

        /// <summary>
        /// Writes a unit test for a class
        /// </summary>
        private readonly ITestWriter _testWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitTestGenerationCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private UnitTestGenerationCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            _syntaxTreeFactory = new SyntaxTreeFactory();
            _classParser = new ClassParser();
            _testWriter = new TestWriter();

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(Execute, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static UnitTestGenerationCommand Instance { get; private set; }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IAsyncServiceProvider ServiceProvider => _package;

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in UnitTestGenerationCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = package.GetService<IMenuCommandService>() as OleMenuCommandService;
            Instance = new UnitTestGenerationCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            var dte = ServiceProvider.GetService<DTE>();

            ThreadHelper.ThrowIfNotOnUIThread();

            if (!dte.ActiveDocument.FullName.EndsWith(".cs")) return;
            var tree = _syntaxTreeFactory.LoadSyntaxTreeFromDocument(dte.ActiveDocument);

            foreach (var classDefinition in _classParser.LoadClass(tree))
            {
                if (classDefinition != null)
                    _testWriter.ScaffoldTest(classDefinition);
            };
        }
    }
}
