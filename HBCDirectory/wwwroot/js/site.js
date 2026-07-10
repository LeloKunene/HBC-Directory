// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("DOMContentLoaded", function () {
  document
    .querySelectorAll(".alert-success.alert-dismissible")
    .forEach(function (alertEl) {
      setTimeout(function () {
        const alert = bootstrap.Alert.getOrCreateInstance(alertEl);
        alert.close();
      }, 3000);
    });
});
