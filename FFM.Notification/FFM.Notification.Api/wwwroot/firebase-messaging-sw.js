// Scripts for firebase messaging service worker
importScripts('https://www.gstatic.com/firebasejs/9.23.0/firebase-app-compat.js');
importScripts('https://www.gstatic.com/firebasejs/9.23.0/firebase-messaging-compat.js');

const firebaseConfig = {
  apiKey: "AIzaSyCtLJlWp4Xulii2rfrc_XSDGruwqFBlCMo",
  authDomain: "ffm-notification-api.firebaseapp.com",
  projectId: "ffm-notification-api",
  storageBucket: "ffm-notification-api.firebasestorage.app",
  messagingSenderId: "214942890435",
  appId: "1:214942890435:web:c56e26b7db3ff057f3a576"
};

firebase.initializeApp(firebaseConfig);
const messaging = firebase.messaging();

messaging.onBackgroundMessage((payload) => {
  console.log('[firebase-messaging-sw.js] Received background message: ', payload);
  const notificationTitle = payload.notification?.title || 'FFM Alert';
  const notificationOptions = {
    body: payload.notification?.body || 'You have a new task or update.',
    icon: 'https://cdn-icons-png.flaticon.com/512/1827/1827392.png',
    data: payload.data
  };

  self.registration.showNotification(notificationTitle, notificationOptions);
});

