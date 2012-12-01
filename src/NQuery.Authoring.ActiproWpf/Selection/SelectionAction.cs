﻿using System;
using System.Collections.Generic;

using ActiproSoftware.Windows.Controls.SyntaxEditor;
using ActiproSoftware.Windows.Controls.SyntaxEditor.Implementation;

using NQuery.Authoring.Selection;

namespace NQuery.Authoring.ActiproWpf.Selection
{
    public abstract class SelectionAction : EditActionBase
    {
        private sealed class SelectionHandler
        {
            private readonly IEditorView _editorView;
            private readonly Stack<TextSpan> _selectionStack = new Stack<TextSpan>();

            public SelectionHandler(IEditorView editorView)
            {
                _editorView = editorView;
                SubscribeToSelectionChanged();
            }

            private void SubscribeToSelectionChanged()
            {
                _editorView.SyntaxEditor.ViewSelectionChanged += SyntaxEditorOnViewSelectionChanged;
            }

            private void UnsubscribeToSelectionChanged()
            {
                _editorView.SyntaxEditor.ViewSelectionChanged -= SyntaxEditorOnViewSelectionChanged;
            }

            private void SyntaxEditorOnViewSelectionChanged(object sender, EditorViewSelectionEventArgs e)
            {
                if (e.View == _editorView)
                    _selectionStack.Clear();
            }

            public async void ExtendSelection()
            {
                var parseData = await _editorView.SyntaxEditor.Document.GetParseDataAsync();
                if (parseData == null)
                    return;

                var syntaxTree = parseData.SyntaxTree;
                var currentSelection = _editorView.Selection.SnapshotRange.ToTextSpan(syntaxTree.TextBuffer);
                var extendedSelection = syntaxTree.ExtendSelection(currentSelection);

                if (currentSelection == extendedSelection)
                    return;

                _selectionStack.Push(currentSelection);
                Select(extendedSelection, parseData);
            }

            public async void ShrinkSelection()
            {
                var parseData = await _editorView.SyntaxEditor.Document.GetParseDataAsync();
                if (parseData == null)
                    return;

                if (_selectionStack.Count == 0)
                    return;

                var newSelection = _selectionStack.Pop();
                Select(newSelection, parseData);
            }

            private void Select(TextSpan selection, NQueryParseData parseData)
            {
                var textBuffer = parseData.SyntaxTree.TextBuffer;
                var snapshot = parseData.Snapshot;
                var snapshotRange = textBuffer.ToSnapshotRange(snapshot, selection);

                UnsubscribeToSelectionChanged();
                _editorView.Selection.SelectRange(snapshotRange);
                SubscribeToSelectionChanged();
            }
        }

        private static SelectionHandler GetSelectionHandler(ITextView textView)
        {
            var editorView = textView as IEditorView;
            if (editorView == null)
                return null;

            var key = typeof(SelectionHandler);
            SelectionHandler value;
            if (!textView.Properties.TryGetValue(key, out value))
            {
                value = new SelectionHandler(editorView);
                textView.Properties.Add(key, value);
            }

            return value;
        }

        protected static void ExtendSelection(ITextView textView)
        {
            var selectionHandler = GetSelectionHandler(textView);
            if (selectionHandler != null)
                selectionHandler.ExtendSelection();
        }

        protected static void ShrinkSelection(ITextView textView)
        {
            var selectionHandler = GetSelectionHandler(textView);
            if (selectionHandler != null)
                selectionHandler.ShrinkSelection();
        }
    }
}