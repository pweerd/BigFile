﻿using Bitmanager.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Bitmanager.Grid {
   /// <summary>
   /// Defines a base class for high performance grid controls.
   /// </summary>
   [System.ComponentModel.DesignerCategory ("")]
   public class RawGrid : Control {
      public delegate Cell ON_GETCELL (Object sender, int row, int col);
      public delegate void ON_SELECTED_INDEX_CHANGED (object sender, EventArgs e);
      public event ON_GETCELL OnGetCell;
      public event EventHandler SelectedIndexChanged;
      protected readonly HScrollBar hScrollBar;
      protected readonly VScrollBar vScrollBar;

      private readonly Graphics _graphics;
      private readonly IntPtr _graphicsHdc;
      private readonly CellBuffer _cellBuffer;
      private readonly DisplayBuffer _displayBuffer;
      private FontManager _fontManager;
      private int _selectedIndex;
      private int _currentIndex;
      private int _rowCount;

      private List<InternalColumn> _columns;

      private int clientWidth, clientHeight; //without scrollbars
      private int maxLineWidth;
      public static readonly Logger logger = Logs.CreateLogger ("bigfile", "grid");



      public RawGrid () {
         _columns = new ();

         hScrollBar = new HScrollBar ();
         hScrollBar.Dock = DockStyle.Bottom;
         vScrollBar = new VScrollBar ();
         vScrollBar.Dock = DockStyle.Right;
         _graphics = CreateGraphics ();
         _graphicsHdc = _graphics.GetHdc ();
         _fontManager = new FontManager ();
         _cellBuffer = new CellBuffer ();
         _displayBuffer = new DisplayBuffer (_graphicsHdc, _fontManager);

         Font = new Font ("Microsoft Sans Serif", 12);
         BackColor = Color.LightGray;

         _fontManager.Load (Font);
         hScrollBar.Scroll += handleScroll;
         vScrollBar.Scroll += handleScroll;
         Controls.Add (hScrollBar);
         Controls.Add (vScrollBar);

         _selectedIndex = -1;
      }
      protected override void OnResize (EventArgs e) {
         clientWidth = Width;
         if (vScrollBar.Visible) clientWidth -= vScrollBar.Width;
         clientHeight = Height;
         if (hScrollBar.Visible) clientHeight -= hScrollBar.Height;
         logger.Log ("OnResize: h={0} ({1}), w={2} ({3})", clientHeight, Height, clientWidth, Width);
         recomputeScrollBars ();
         Invalidate ();
         base.OnResize (e);
      }

      public int SelectedIndex {
         get { return _selectedIndex; }
         set {
            int ix;
            if (value < 0) ix = -1;
            else ix = (value >= RowCount) ? RowCount - 1 : value;
            if (ix != _selectedIndex) {
               _selectedIndex = ix;
               OnSelectedIndexChanged (ix);
               Invalidate ();
            }
         }
      }

      protected virtual void OnSelectedIndexChanged(int ix) {
         if (SelectedIndexChanged != null) {
            SelectedIndexChanged (this, EventArgs.Empty);
         }
      }


      public UpdateableColumns Columns => new UpdateableColumns (this, _columns);


      internal void CreateInternalColumns (List<Column> list) {
         List<InternalColumn> tmp = new List<InternalColumn> (list.Count);
         int offset = 0;
         for (int i = 0; i < list.Count; i++) {
            var c = list[i];
            tmp.Add (new InternalColumn (this, c, offset));
            offset += c.Width;
         }
         _columns = tmp;
         ColumnsChanged?.Invoke (this, EventArgs.Empty);

         DumpColumns ();

      }

      bool updatingScrollbar;
      private void handleScroll (object sender, ScrollEventArgs e) {
         if (!updatingScrollbar) {
            if (e.ScrollOrientation == ScrollOrientation.VerticalScroll) {
               this.VerticalOffset = (long)(e.NewValue * vScrollBarMultiplier);
            } else {
               this.HorizontalOffset = (long)(e.NewValue * hScrollBarMultiplier);
            }
            logger.Log ("e.scroll={0}, orient={1}, offset={2}", e.NewValue, e.ScrollOrientation, VerticalOffset);
         }
      }

      public int RowCount { get { return _rowCount; }
         set {
            _rowCount = value < 0 ? 0: value;
            recomputeScrollBars ();
            logger.Log ("min={0}, max={1}, multiplier={2}", vScrollBar.Minimum, vScrollBar.Maximum, vScrollBarMultiplier);
            Invalidate ();
         }
      }

      double hScrollBarMultiplier=1;
      double vScrollBarMultiplier;
      private void recomputeScrollBars() {
         const int MAX_VALUE = (int.MaxValue - 10000) & ~16;
         if (clientHeight <= 0 || clientWidth <= 0) return;

         //Reconstruct vertical scrollbar
         vScrollBarMultiplier = 1.0;
         int max = MAX_VALUE; 
         int rowHeight = RowHeight;
         int rowsPerWindow = (clientHeight+rowHeight-1) / rowHeight;

         long neededPixels = (rowHeight * (long)_rowCount) - clientHeight;
         logger.Log ("Needed pixels (H): {0}, c={1}, rh={2}", neededPixels, _rowCount, rowHeight);
         if (neededPixels <= 0) {
            vScrollBar.Enabled = false;
            return;
         }
         neededPixels += rowHeight / 2;
         vScrollBar.Enabled = true;
         if (neededPixels < max)
            max = (int)neededPixels;
         else {
            vScrollBarMultiplier = (neededPixels) / (double)max;
         }

         int smallChange = (int)(.99999 + rowHeight / vScrollBarMultiplier);
         int largeChange = (int)(((rowsPerWindow - 1) * rowHeight) / vScrollBarMultiplier);
         vScrollBar.SmallChange = smallChange;
         vScrollBar.LargeChange = largeChange < smallChange ? smallChange:largeChange;
         logger.Log ("Max (V): {0}, mult={1}", max, vScrollBarMultiplier);
         vScrollBar.Maximum = max + vScrollBar.LargeChange;

         //Reconstruct horizontal bar
         hScrollBarMultiplier = 1.0;
         max = MAX_VALUE;
         neededPixels = _columns.Count == 0 ? 0 : _columns[^1].GlobalOffsetPlusWidth - clientWidth;
         if (neededPixels <= 0) {
            hScrollBar.Enabled = false;
            return;
         }
         hScrollBar.Enabled = true;
         if (neededPixels < max)
            max = (int)neededPixels;
         else {
            hScrollBarMultiplier = (neededPixels) / (double)max;
         }
         smallChange = clientWidth / 10;
         largeChange = clientWidth - 10 * rowHeight;
         hScrollBar.SmallChange = smallChange;
         hScrollBar.LargeChange = largeChange < smallChange ? smallChange : largeChange; ;
         logger.Log ("Max (H): {0}, mult={1}", max, hScrollBarMultiplier);
         hScrollBar.Maximum = max - clientWidth;
      }


      private void DumpColumns () {
         logger.Log ("Dumping columns:");
         for (int i = 0; i < _columns.Count; i++) {
            var c = _columns[i];
            logger.Log ("-- [{0}]: w={1}, offs={2}", i, c.Width, c.GlobalOffset);
         }

      }


      private long _horizontalOffset;
      /// <summary>
      /// Horizontal offset applied to the content of the control.
      /// It's highly recommended to use this property over placing the <c>Grid</c> in a scrollable panel. This way the grid can avoid processing and rendering of invisible columns (columns that are out of the scrolling area).
      /// </summary>
      public long HorizontalOffset {
         get => _horizontalOffset;
         set {
            if (_horizontalOffset == value) return;
            _horizontalOffset = value < 0 ? 0 : value;
            updateScrollbarPosition (hScrollBar, _horizontalOffset / hScrollBarMultiplier);

            HorizontalOffsetChanged?.Invoke (this, EventArgs.Empty);
            Invalidate ();
         }
      }

      private long _verticalOffset;
      /// <summary>
      /// Vertical offset applied to the content of the control.
      /// It's highly recommended to use this property over placing the <c>Grid</c> in a scrollable panel. This way the grid can avoid processing and rendering of invisible rows (rows that are out of the scrolling area).
      /// </summary>
      public long VerticalOffset {
         get => _verticalOffset;
         set {
            if (_verticalOffset == value) return;
            _verticalOffset = value < 0 ? 0 : value;
            updateScrollbarPosition (vScrollBar, _verticalOffset / vScrollBarMultiplier);

            VerticalOffsetChanged?.Invoke (this, EventArgs.Empty);
            Invalidate ();
         }
      }

      private void updateScrollbarPosition (ScrollBar sb, double pos) {
         updatingScrollbar = true;
         try {
            int newValue = (int)(.5 + pos);
            if (newValue < sb.Minimum) newValue = sb.Minimum;
            else if (newValue > sb.Maximum) newValue = sb.Maximum;
            sb.Value = newValue;
         } finally {
            updatingScrollbar = false;
         }
      }

      /// <summary>
      /// Height of a single row. This height includes a cell height and one extra pixel reserved for a bottom border.
      /// </summary>
      /// <remarks>
      /// This is a read only property. The height of a cell is derived directly from the font size and can by altered by changing it.
      /// </remarks>
      public int RowHeight => _fontManager.FontHeight + 1;


      protected override void Dispose (bool disposing) {
         if (disposing) {
            _graphics.Dispose ();
            _displayBuffer.Dispose ();
            _fontManager.Dispose ();
         }

         base.Dispose (disposing);
      }

      /// <summary>
      /// A virtual method to be overwritten by a deriving class to provides the cell content and layout for requested cell coordinates.
      /// </summary>
      protected virtual Cell GetCell (int row, int col) {
         return (OnGetCell != null) ? OnGetCell(this, row, col) : CreateCell (col);
      }

      public virtual Cell CreateCell(int col) {
         return col < _columns.Count ? new Cell (_columns[col]) : new Cell(this);
      }
      public void SetColumnWidth (int col, int width) {
         InternalColumn.SetColumnWidth (_columns, col, width);
         Invalidate ();
      }

      public int MeasureTextWidth (String txt) {
//         var g = new Graphics (_graphicsHdc);

         if (String.IsNullOrEmpty (txt)) return 0;

         return Gdi32.Measure (_graphicsHdc, txt, _fontManager.GetHdc (FontStyle.Regular)).Width;
      }



      protected override void OnPaint (PaintEventArgs e) {
         long virtualX = e.ClipRectangle.X + HorizontalOffset;
         long virtualY = e.ClipRectangle.Y + VerticalOffset;
         long virtualXEnd = virtualX + e.ClipRectangle.Width;

         long rh = RowHeight;
         int rowStart = (int)(virtualY / rh);
         int rowEnd = 1+(int)((virtualY + e.ClipRectangle.Width) / rh);

         int colStart = 0;
         for (; colStart < _columns.Count && _columns[colStart].GlobalOffsetPlusWidth <= virtualX; colStart++) ;
         int colEnd = _columns.Count;
         for (; colEnd > colStart && _columns[colEnd-1].GlobalOffset > virtualXEnd; colEnd--) ;

         //logger.Log ("clip=({0}, {1}), ({2}, {3})", e.ClipRectangle.X, e.ClipRectangle.Y, e.ClipRectangle.Width, e.ClipRectangle.Height);
         _displayBuffer.Init (e.ClipRectangle.Height, e.ClipRectangle.Width);
         _displayBuffer.Clear (BackColor);
         //logger.Log ("rows=({0}, {1}), cols=({2}, {3})", rowStart, rowEnd, colStart, colEnd);

         Rectangle bufRect = new Rectangle (0, 0, 0, (int)rh);
         for (int col = colStart; col < colEnd; col++) {
            var column = _columns[col];
            int x = (int)(column.GlobalOffset - virtualX);
            bufRect.X = x;
            bufRect.Width = column.Width;

            int y=(int)(rowStart * rh - virtualY);
            for (int row = rowStart; row < rowEnd; row++) {
//               logger.Log ("-- getCell ({0}, {1}", row, col);
//               logger.Log ("-- cellRect=({0}, {1}), ({2}, {3})", bufRect.X, bufRect.Y, bufRect.Width, bufRect.Height);
               Cell cell = GetCell (row, col);
               //_displayBuffer.ClearRect (cell.BackgroundColor ?? this.BackColor, bufRect);
               bufRect.Y = y;
               _displayBuffer.PrintCell (
                  cell.BackColor,
                  cell.ForeColor,
                  bufRect,
                  cell.FontStyle,
                  cell.Alignment,
                  cell.Text
               );
               y += (int)rh;
            }
         }
         Gdi32.BitBlt (_graphicsHdc, e.ClipRectangle.X, e.ClipRectangle.Y, e.ClipRectangle.Width, e.ClipRectangle.Height, _displayBuffer.Hdc, 0, 0, Gdi32.TernaryRasterOperations.SRCCOPY);
      }

      public void GotoCell (int row, int col=-1, bool select=true) {
         if (row >= 0) VerticalOffset = RowHeight * (long)row;
         if (col >= 0 && col < _columns.Count) HorizontalOffset = _columns[col].GlobalOffset;
         if (select) SelectedIndex = row;
         _currentIndex = row;
         Invalidate ();
      }

      public void MakeCellVisible (int row, int col = -1, bool select = true) {
         int visibleRows = clientHeight / RowHeight;
         if (row < 0) row = 0;
         else if (row >= _rowCount) row = _rowCount - 1;

         long vOffset = RowHeight * (long)row;
         long offset = vOffset - _verticalOffset;

         if (visibleRows < 3) {
            VerticalOffset = vOffset;
            goto SELECT;
         }
         if (vOffset < _verticalOffset) {
            vOffset -= 2 * RowHeight;
            VerticalOffset = vOffset;
            goto SELECT;
         }
         if (vOffset + RowHeight > clientHeight) {
            vOffset = VerticalOffset + clientHeight - 3 * RowHeight;
            VerticalOffset = vOffset;
            goto SELECT;
         }

      SELECT:
         if (select) SelectedIndex = row;

      MAKE_COL_VISIBLE:
         if (col < 0 ) goto EXIT_RTN;
         if (col >= _columns.Count) col = _columns.Count - 1;
         HorizontalOffset = _columns[col].GlobalOffset;

      EXIT_RTN:
         return;
      }

      protected override bool IsInputKey (Keys keyData) {
         bool ret;
         switch (keyData & Keys.KeyCode) {
            case Keys.Left:
            case Keys.Right:
            case Keys.Up:
            case Keys.Down:
            case Keys.PageUp:
            case Keys.PageDown:
            case Keys.Home:
            case Keys.End:
               ret = true; break;
            default: ret = base.IsInputKey (keyData); break;
         }
         logger.Log ("Old IsInputKey ({0}) -> {1} (was {2})", keyData, ret, base.IsInputKey (keyData));
         return ret;
      }

      protected override void OnMouseWheel (MouseEventArgs e) {
         base.OnMouseWheel (e);
         int delta = 1; // e.Delta < 0 ? -1 : 1;
         int visibleRows = clientHeight / RowHeight;
         int max = Math.Min(1, visibleRows / 2);
         if (RowCount > 1000) delta *= 3;
         else if (RowCount > 100) delta *= 2;

         if (max > delta) delta = max;
         if (e.Delta > 0) delta = -delta;

         VerticalOffset = VerticalOffset + delta*RowHeight;
      }

      protected override void OnKeyDown (KeyEventArgs e) {
         int row;
         base.OnKeyDown (e);
         if (e.Handled || _rowCount==0 || _columns.Count==0) return;
         switch (e.KeyCode) {
            case Keys.Home:
               HorizontalOffset = 0;
               if (e.Control) VerticalOffset = 0;
               break;
            case Keys.End:
               if (e.Control) {
                  VerticalOffset = RowHeight * (long)RowCount - clientHeight;
                  HorizontalOffset = 0;
               } else {
                  logger.Log ("offset1={0}, offset2={1}", _columns[^1].GlobalOffsetPlusWidth - clientWidth, (int)(.5 + hScrollBar.Maximum * hScrollBarMultiplier));
                  HorizontalOffset = _columns[^1].GlobalOffsetPlusWidth - clientWidth;// (int) (.5 + hScrollBar.Maximum * hScrollBarMultiplier);
               }
               break;
            case Keys.Up:
               row = _currentIndex - 1;
               if (row < 0) row = 0;
               _currentIndex = row;
               MakeCellVisible (row, -1, true);
               break;

            case Keys.Down:
               row = _currentIndex + 1;
               if (row >= _rowCount) row = _rowCount-1;
               _currentIndex = row;
               MakeCellVisible (row, -1, true);
               break;
               //case Keys.PageUp:
               //case Keys.PageDown:
         }
      }
      protected override void OnKeyUp (KeyEventArgs e) {
         base.OnKeyUp (e);
      }
      protected override void OnKeyPress (KeyPressEventArgs e) {
         base.OnKeyPress (e);
      }
      protected override void OnPaintBackground (PaintEventArgs e) { }

      protected override void OnBackColorChanged (EventArgs e) {
         base.OnBackColorChanged (e);

         Invalidate ();
      }

      protected override void OnFontChanged (EventArgs e) {
         _fontManager.Load (Font);
         Invalidate ();
         base.OnFontChanged (e);
      }

      private int getRow (int y) {
         int row = (int)((y + VerticalOffset) % RowHeight);
         return (row >= 0 && row < _rowCount) ? row : -1;
      }
      private int getCell (int x) {
         long virtualX = x + HorizontalOffset;
         for (int i=0; i<_columns.Count; i++) {
            var c = _columns[i];
            if (virtualX >= c.GlobalOffsetPlusWidth) continue;
            if (virtualX < c.GlobalOffset) continue;
            return i;
         }
         return -1;
      }


      public int GetMouseRow (int y) {
         int row = (int)((_verticalOffset + y) / RowHeight);
         if (row < 0) row = -1;
         else if (row >= RowCount) row = RowCount - 1;
         return row;
      }
      public int GetMouseRowAndCol (int x, int y, out int col) {
         long virtualX = x + HorizontalOffset;
         for (int i = 0; i < _columns.Count; i++) {
            var c = _columns[i];
            if (virtualX >= c.GlobalOffsetPlusWidth) continue;
            if (virtualX < c.GlobalOffset) continue;
            col = i;
            goto EXIT_RTN;
         }
         col = -1;

      EXIT_RTN:
         return GetMouseRow (y);
      }


      public event EventHandler<EventArgs> ColumnsChanged;
      public event EventHandler<EventArgs> HorizontalOffsetChanged;
      public event EventHandler<EventArgs> VerticalOffsetChanged;
   }
}
