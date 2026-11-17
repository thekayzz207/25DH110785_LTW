"use strict";
(function () {
  function initialize() {
    var menus = Array.prototype.slice.call(
      document.querySelectorAll("[data-account-menu]"),
    );
    if (!menus.length) return;

    function close(menu, restoreFocus) {
      menu.open = false;
      if (restoreFocus) {
        var toggle = menu.querySelector("summary");
        if (toggle) toggle.focus();
      }
    }

    document.addEventListener("click", function (event) {
      menus.forEach(function (menu) {
        if (menu.open && !menu.contains(event.target)) close(menu, false);
      });
    });

    document.addEventListener("keydown", function (event) {
      if (event.key !== "Escape") return;
      menus.forEach(function (menu) {
        if (menu.open) {
          var restoreFocus = menu.contains(document.activeElement);
          close(menu, restoreFocus);
          event.preventDefault();
        }
      });
    });

    document.addEventListener("focusin", function (event) {
      menus.forEach(function (menu) {
        if (menu.open && !menu.contains(event.target)) close(menu, false);
      });
    });
  }

  if (document.readyState === "loading")
    document.addEventListener("DOMContentLoaded", initialize);
  else initialize();
})();
